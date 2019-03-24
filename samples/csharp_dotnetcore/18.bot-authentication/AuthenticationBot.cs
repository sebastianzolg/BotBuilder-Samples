// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuthenticationBot;
using Bot_Authentication;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;

namespace Microsoft.BotBuilderSamples
{
    /// <summary>
    /// Represents a bot that processes incoming activities.
    /// For each user interaction, an instance of this class is created and the OnTurnAsync method is called.
    /// This is a Transient lifetime service.  Transient lifetime services are created
    /// each time they're requested. For each Activity received, a new instance of this
    /// class is created. Objects that are expensive to construct, or have a lifetime
    /// beyond the single turn, should be carefully managed.
    /// For example, the <see cref="MemoryStorage"/> object and associated
    /// <see cref="IStatePropertyAccessor{T}"/> object are created with a singleton lifetime.
    /// </summary>
    /// <seealso cref="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-2.1"/>
    public class AuthenticationBot : IBot
    {
        // The connection name here must match the one from
        // your Bot Channels Registration on the settings blade in Azure.
        private const string ConnectionName = "";

        private const string ConfirmPromptName = "confirmPrompt";
        private const string ConfirmPromptPhase2Name = "confirmPromptPhase2";

        private const string WelcomeText = @"This bot will introduce you to Authentication.
                                        Type anything to get logged in. Type 'logout' to sign-out.
                                        Type 'help' to view this message again";

        private readonly AuthenticationBotAccessors _stateAccessors;
        private readonly DialogSet _dialogs;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticationBot"/> class.
        /// </summary>
        /// <param name="accessors">A class containing <see cref="IStatePropertyAccessor{T}"/> used to manage state.</param>
        /// <seealso cref="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-2.1#windows-eventlog-provider"/>
        public AuthenticationBot(AuthenticationBotAccessors accessors)
        {
            _stateAccessors = accessors ?? throw new ArgumentNullException(nameof(accessors));
            _dialogs = new DialogSet(_stateAccessors.ConversationDialogState);

            // Add the OAuth prompts and related dialogs into the dialog set
            _dialogs.Add(new AuthenticationDialog(ConnectionName));
            _dialogs.Add(new ConfirmPrompt(ConfirmPromptName));
            _dialogs.Add(new ConfirmPrompt(ConfirmPromptPhase2Name));
            _dialogs.Add(new WaterfallDialog("authFlow", new WaterfallStep[] { LoginFirstTimeStepAsync, LoggedInStepAsync, AquireFreshTokenStepAsync, DisplayFreshlyAquiredTokenAsync, DisplayTokenPhase2Async }));
        }

        /// <summary>
        /// Every conversation turn for our Echo Bot will call this method.
        /// </summary>
        /// <param name="turnContext">A <see cref="ITurnContext"/> containing all the data needed
        /// for processing this conversation turn. </param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> that represents the work queued to execute.</returns>
        /// <seealso cref="BotStateSet"/>
        /// <seealso cref="ConversationState"/>
        /// <seealso cref="IMiddleware"/>
        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            var dc = await _dialogs.CreateContextAsync(turnContext, cancellationToken);
            if (turnContext == null)
            {
                throw new ArgumentNullException(nameof(turnContext));
            }

            switch (turnContext.Activity.Type)
            {
                case ActivityTypes.Message:

                    // This bot is not case sensitive.
                    var text = turnContext.Activity.Text.ToLowerInvariant();
                    if (text == "help")
                    {
                        await turnContext.SendActivityAsync(WelcomeText, cancellationToken: cancellationToken);
                        break;
                    }

                    if (text == "logout")
                    {
                        // The bot adapter encapsulates the authentication processes.
                        var botAdapter = (BotFrameworkAdapter)turnContext.Adapter;
                        await botAdapter.SignOutUserAsync(turnContext, ConnectionName, cancellationToken: cancellationToken);
                        await turnContext.SendActivityAsync("You have been signed out.", cancellationToken: cancellationToken);
                        await turnContext.SendActivityAsync(WelcomeText, cancellationToken: cancellationToken);
                        break;
                    }

                    await dc.ContinueDialogAsync(cancellationToken);

                    if (!turnContext.Responded)
                    {
                        // Start the Login process.
                        await dc.BeginDialogAsync("authFlow", cancellationToken: cancellationToken);
                    }

                    break;
                case ActivityTypes.Event:
                case ActivityTypes.Invoke:
                    // This handles the MS Teams Invoke Activity sent when magic code is not used.
                    // See: https://docs.microsoft.com/en-us/microsoftteams/platform/concepts/authentication/auth-oauth-card#getting-started-with-oauthcard-in-teams
                    // The Teams manifest schema is found here: https://docs.microsoft.com/en-us/microsoftteams/platform/resources/schema/manifest-schema
                    // It also handles the Event Activity sent from the emulator when the magic code is not used.
                    // See: https://blog.botframework.com/2018/08/28/testing-authentication-to-your-bot-using-the-bot-framework-emulator/
                    dc = await _dialogs.CreateContextAsync(turnContext, cancellationToken);
                    await dc.ContinueDialogAsync(cancellationToken);
                    if (!turnContext.Responded)
                    {
                        await dc.BeginDialogAsync("authDialog", cancellationToken: cancellationToken);
                    }

                    break;
                case ActivityTypes.ConversationUpdate:
                    // Send a welcome & help message to the user.
                    if (turnContext.Activity.MembersAdded != null)
                    {
                        await SendWelcomeMessageAsync(turnContext, cancellationToken);
                    }

                    break;
            }
        }

        /// <summary>
        /// Greet new users as they are added to the conversation.
        /// </summary>
        /// <param name="turnContext">Provides the <see cref="ITurnContext"/> for the turn of the bot.</param>
        /// <param name="cancellationToken" >(Optional) A <see cref="CancellationToken"/> that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> representing the operation result of the Turn operation.</returns>
        private static async Task SendWelcomeMessageAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            foreach (var member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(
                        $"Welcome to AuthenticationBot {member.Name}. {WelcomeText}",
                        cancellationToken: cancellationToken);
                }
            }
        }

        /// <summary>
        /// This <see cref="WaterfallStep"/> prompts the user to log in.
        /// </summary>
        /// <param name="step">A <see cref="WaterfallStepContext"/> provides context for the current waterfall step.</param>
        /// <param name="cancellationToken" >(Optional) A <see cref="CancellationToken"/> that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> representing the operation result of the operation.</returns>
        private static async Task<DialogTurnResult> LoginFirstTimeStepAsync(WaterfallStepContext step, CancellationToken cancellationToken)
        {
            return await step.BeginDialogAsync(nameof(AuthenticationDialog), cancellationToken: cancellationToken);
        }

        /// <summary>
        /// This <see cref="WaterfallStep"/> prompts the user to log in.
        /// </summary>
        /// <param name="step">A <see cref="WaterfallStepContext"/> provides context for the current waterfall step.</param>
        /// <param name="cancellationToken" >(Optional) A <see cref="CancellationToken"/> that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> representing the operation result of the operation.</returns>
        private static async Task<DialogTurnResult> AquireFreshTokenStepAsync(WaterfallStepContext step, CancellationToken cancellationToken)
        {
            // There is no difference between LoginFirstTimeStepAsync but we're passing the previous step result as options to the AuthFlow
            // so we can maintain it for the next regular step in the waterfall
            return await step.BeginDialogAsync(nameof(AuthenticationDialog), step.Result, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// In this step we check that a token was received and prompt the user as needed.
        /// </summary>
        /// <param name="step">A <see cref="WaterfallStepContext"/> provides context for the current waterfall step.</param>
        /// <param name="cancellationToken" >(Optional) A <see cref="CancellationToken"/> that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> representing the operation result of the operation.</returns>
        private static async Task<DialogTurnResult> LoggedInStepAsync(WaterfallStepContext step, CancellationToken cancellationToken)
        {
            // Get the token from the previous step. Note that we could also have gotten the
            // token directly from the prompt itself. There is an example of this in the next method.
            var authResponse = (AuthenticationStepResult)step.Result;
            var tokenResponse = authResponse.TokenResponse;
            if (tokenResponse != null)
            {
                await step.Context.SendActivityAsync("You are now logged in.", cancellationToken: cancellationToken);
                return await step.PromptAsync(
                    ConfirmPromptName,
                    new PromptOptions
                    {
                        Prompt = MessageFactory.Text("Would you like to view your token?"),
                        Choices = new List<Choice> { new Choice("Yes"), new Choice("No") },
                    },
                    cancellationToken);
            }

            await step.Context.SendActivityAsync("Login was not successful please try again.", cancellationToken: cancellationToken);
            return Dialog.EndOfTurn;
        }

        /// <summary>
        /// Fetch the token and display it for the user if they asked to see it.
        /// </summary>
        /// <param name="step">A <see cref="WaterfallStepContext"/> provides context for the current waterfall step.</param>
        /// <param name="cancellationToken" >(Optional) A <see cref="CancellationToken"/> that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> representing the operation result of the operation.</returns>
        private static async Task<DialogTurnResult> DisplayFreshlyAquiredTokenAsync(WaterfallStepContext step, CancellationToken cancellationToken)
        {
            var authResult = (AuthenticationStepResult)step.Result;

            // Verify user want's to display the token
            var result = (bool)authResult.StepResult;
            if (result)
            {
                // We not just show the previous token, instead we aquire a fresh one once again.
                var prompt = await step.BeginDialogAsync(nameof(AuthenticationDialog), result, cancellationToken: cancellationToken);
            }

            return Dialog.EndOfTurn;
        }

        private static async Task<DialogTurnResult> DisplayTokenPhase2Async(WaterfallStepContext step, CancellationToken cancellationToken)
        {
            var authResult = step.Result as AuthenticationStepResult;

            // This is the result of the step before the authentication flow. This way we have access to it and don't need to reprompt
            // after we aquired a fresh access token.
            var result = (bool)authResult.StepResult;

            if (result)
            {
                var tokenResponse = authResult.TokenResponse;

                await step.Context.SendActivityAsync($"Here is your token {tokenResponse.Token}", cancellationToken: cancellationToken);
            }

            return Dialog.EndOfTurn;
        }
    }
}
