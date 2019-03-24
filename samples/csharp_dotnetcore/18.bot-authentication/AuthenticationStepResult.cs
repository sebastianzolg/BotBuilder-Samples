using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Bot.Schema;

namespace AuthenticationBot
{
    public class AuthenticationStepResult
    {
        public readonly TokenResponse TokenResponse;
        public readonly object StepResult;

        public AuthenticationStepResult(TokenResponse tokenResponse, object stepData)
        {
            TokenResponse = tokenResponse;
            StepResult = stepData;
        }
    }
}
