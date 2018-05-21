using System;
using System.Threading.Tasks;

using Microsoft.Bot.Connector;
using Microsoft.Bot.Builder.Dialogs;
using System.Net.Http;
using System.Configuration;
using Newtonsoft.Json.Linq;

namespace Microsoft.Bot.Sample.SimpleEchoBot
{
    [Serializable]
    public class EchoDialog : IDialog<object>
    {
        private static string ConnectionName = ConfigurationManager.AppSettings["ConnectionName"];
        private int _reties;
        private string _retryMessage = "Login not success, try again!";

        public async Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceivedAsync);
        }

        public async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> argument)
        {
            var message = await argument;

            //signout flow too
            if (message.Text.Equals("signout", StringComparison.InvariantCultureIgnoreCase))
            {
                await context.SignOutUserAsync(ConnectionName);
                await context.PostAsync("You have been signed out.");
                
            }
            else
            {
                var token = await context.GetUserTokenAsync(ConnectionName).ConfigureAwait(false);

                if (token != null)
                {
                    
                    //Send to other dialogs , token is there
                    await context.PostAsync("Hello, This is first dialog after authetication success!");
                    context.Wait(MessageReceivedAsync);
                }
                else
                {
                    await SendOAuthCardAsync(context, (Activity)context.Activity);                 
                }
            }
                  
            
          
        }

        private async Task SendOAuthCardAsync(IDialogContext context,Activity activity)
        {
            await context.PostAsync("Seems you are not logged in, do it below :)");

            //Uncomment below for normal oauth, but magic number being generated here

            //To make is appear on web chat
            var reply = await context.Activity.CreateOAuthReplyAsync(ConnectionName, "Sign in here :)", "CLICK ME!", asSignInCard: true);
            await context.PostAsync(reply);
            await context.PostAsync("Paste the OTP (if any) from the authentication screen");
            context.Wait(WaitForToken);

            //context.Wait(MessageReceivedAsync);
        }

        private async Task WaitForToken(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            
            var tokenResponse = activity.ReadTokenResponseContent();
            string verificationCode = null;
            if (tokenResponse != null)
            {
                context.Done(new GetTokenResponse() { Token = tokenResponse.Token });
                await context.PostAsync("Login Success! How may I help you?");
                return;
            }
            else if (activity.IsTeamsVerificationInvoke())
            {
                JObject value = activity.Value as JObject;
                if (value != null)
                {
                    verificationCode = (string)(value["state"]);
                }
            }
            else if (!string.IsNullOrEmpty(activity.Text))
            {
                verificationCode = activity.Text;
            }

            tokenResponse = await context.GetUserTokenAsync(ConnectionName, verificationCode);
            if (tokenResponse != null)
            {
                await context.PostAsync("Login Success!");
                context.Done(new GetTokenResponse() { Token = tokenResponse.Token });
                return;
            }

            // decide whether to retry or not
            if (_reties > 0)
            {
                _reties--;
                await context.PostAsync(_retryMessage);
                await SendOAuthCardAsync(context, activity);
            }
            else
            {
                
                context.Done(new GetTokenResponse() { NonTokenResponse = activity.Text });
                return;
            }
        }
    }



   


}
