using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;

namespace AuthenticationBot
{
    public class AuthenticationDialog : ComponentDialog
    {
        public const string AuthenticationWaterfall = "authenticationWaterfall";
        public const string LoginPrompt = "loginPrompt";

        private string ConnectionName { get; set; }

        public AuthenticationDialog(string connectionName)
            : base(nameof(AuthenticationDialog))
        {
            InitialDialogId = AuthenticationWaterfall;

            var authenticationSteps = new WaterfallStep[]
            {
                PromptToLoginAsync,
                FinishLoginAsync,
            };

            AddDialog(new WaterfallDialog(AuthenticationWaterfall, authenticationSteps));
            AddDialog(new OAuthPrompt(LoginPrompt, new OAuthPromptSettings()
            {
                ConnectionName = connectionName,
                Title = "Sign-in",
                Text = "Please log in",
            }));
        }

        public async Task<DialogTurnResult> PromptToLoginAsync(WaterfallStepContext sc,  CancellationToken cancellationToken)
        {
            return await sc.PromptAsync(LoginPrompt, new PromptOptions());
        }

        public async Task<DialogTurnResult> FinishLoginAsync(WaterfallStepContext sc, CancellationToken cancellationToken)
        {
            var activity = sc.Context.Activity;
            var tokenResponse = sc.Result as TokenResponse;

            return await sc.EndDialogAsync(new AuthenticationStepResult(tokenResponse, sc.Options), cancellationToken);
        }
    }
}
