using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CloudScheduler.Function
{
    public static class DurableFunction
    {
        [FunctionName(nameof(RunOrchestrator))]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context)
        {
            var outputs = new List<string>();

            context.SetCustomStatus("first stage");

            // Replace "hello" with the name of your Durable Activity Function.
            outputs.Add(await context.CallActivityAsync<string>(nameof(FunctionSayHello), "Tokyo"));

            context.SetCustomStatus("first stage done");

            // sleep for 10 seconds between cleanups
            DateTime nextCleanup = context.CurrentUtcDateTime.AddSeconds(5);
            await context.CreateTimer(nextCleanup, CancellationToken.None);

            outputs.Add(await context.CallActivityAsync<string>(nameof(FunctionSayHello), "Seattle"));

            context.SetCustomStatus("second stage");

            nextCleanup = context.CurrentUtcDateTime.AddSeconds(5);
            await context.CreateTimer(nextCleanup, CancellationToken.None);

            context.SetCustomStatus("second stage trying");

            outputs.Add(await context.CallActivityAsync<string>(nameof(FunctionSayHello), "London"));

            context.SetCustomStatus("second stage done");

            nextCleanup = context.CurrentUtcDateTime.AddSeconds(10);
            await context.CreateTimer(nextCleanup, CancellationToken.None);

            context.SetCustomStatus("third stage");

            // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
            return outputs;
        }

        [FunctionName(nameof(FunctionSayHello))]
        public static string FunctionSayHello([ActivityTrigger]string name, ILogger log)
        {
            log.LogInformation($"Saying hello to {name}.");
            return $"Hello {name}!";
        }

        [FunctionName(nameof(HttpOrchestrationStart))]
        public static async Task<HttpResponseMessage> HttpOrchestrationStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync(nameof(RunOrchestrator), null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            var response = starter.CreateCheckStatusResponse(req, instanceId);

            return response;
        }











        const string ApprovalEventName = "ApprovalEvent";
        const string RejectedEventName = "RejectedEvent";

        [FunctionName(nameof(RunApprovalOrchestrator))]
        public static async Task RunApprovalOrchestrator([OrchestrationTrigger] DurableOrchestrationContext context, ILogger log)
        {
            await context.CallActivityAsync(nameof(RequestApproval), null);

            using (var timeoutCts = new CancellationTokenSource())
            {
                DateTime dueTime = context.CurrentUtcDateTime.AddSeconds(20);
                Task durableTimeout = context.CreateTimer(dueTime, timeoutCts.Token);

                Task approvalEvent = context.WaitForExternalEvent(ApprovalEventName);
                Task rejectEvent = context.WaitForExternalEvent(RejectedEventName);

                var triggeredEvent = await Task.WhenAny(approvalEvent, rejectEvent, durableTimeout);

                if (triggeredEvent == approvalEvent)
                {
                    log.LogInformation($"approvalEvent triggered");
                    timeoutCts.Cancel();
                    await context.CallActivityAsync(nameof(ProcessApproval), true);
                }
                else if (triggeredEvent == rejectEvent)
                {
                    log.LogInformation($"rejectEvent triggered");
                    timeoutCts.Cancel();
                    await context.CallActivityAsync(nameof(ProcessApproval), false);
                }
                else
                {
                    await context.CallActivityAsync(nameof(Escalate), null);
                    // go into a loop here.. could use eternal orchestration flow
                    // as described here: https://docs.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-eternal-orchestrations
                }
            }
        }

        [FunctionName(nameof(RequestApproval))]
        public async static Task RequestApproval([ActivityTrigger]DurableActivityContext context, ILogger log)
        {
            log.LogWarning($"trigger RequestApproval in Slack");

            var host = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");
            log.LogInformation($"host {host}");

            var slackHookUrl = Environment.GetEnvironmentVariable("SLACK_HOOK_URL");

            await SlackHelper.SendSlackNotification(slackHookUrl,
                $"Is this picture pornographic?",
                imageUrl: "https://appsfactoryuploads.blob.core.windows.net/public/pics/gerkindick.jpg",
                actions: new List<SlackMessageAttachmentAction>
                {
                    new SlackMessageAttachmentAction {
                        // we don't set an URL for the Action, as this would mean a GET request and a redirect to a browser tab, which sjows the OK response
                        // instead the button will invoke a POST request against the callback URL that must be configured in the App whose WebHook URL was used to post the Slack message
                        Type = "button",
                        Name = $"{context.InstanceId}|{RejectedEventName}",
                        Text = "Reject",
                        Style = "danger",
                        Value = $"{context.InstanceId}|{RejectedEventName}",
                    },
                    new SlackMessageAttachmentAction {
                        Type = "button",
                        Name = $"{context.InstanceId}|{ApprovalEventName}",
                        Text = "Approve",
                        Style = "primary",
                        Value = $"{context.InstanceId}|{ApprovalEventName}",
                    },
                });
        }

        [FunctionName(nameof(ProcessApproval))]
        public async static Task ProcessApproval([ActivityTrigger]DurableActivityContext context, ILogger log)
        {
            var approved = context.GetInput<bool>();

            log.LogWarning($"process was {(approved ? "approved" : "rejected")}.");

            var slackHookUrl = Environment.GetEnvironmentVariable("SLACK_HOOK_URL");
            await SlackHelper.SendSlackNotification(slackHookUrl, $"Picture was {(approved ? "approved" : "rejected")}");
        }

        [FunctionName(nameof(Escalate))]
        public async static Task Escalate([ActivityTrigger]DurableActivityContext context, ILogger log)
        {
            log.LogWarning($"Escalate!!");

            var slackHookUrl = Environment.GetEnvironmentVariable("SLACK_HOOK_URL");
            await SlackHelper.SendSlackNotification(slackHookUrl, $"Picture wasn't approved/rejected yet, this is a reminder..");
        }







        [FunctionName(nameof(HttpInteractionOrchestrationStart))]
        public static async Task<HttpResponseMessage> HttpInteractionOrchestrationStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            ILogger log)
        {
            var slackHookUrl = Environment.GetEnvironmentVariable("SLACK_HOOK_URL");
            if (slackHookUrl == null)
            {
                return new HttpResponseMessage { StatusCode = System.Net.HttpStatusCode.BadRequest, Content = new StringContent("SLACK_HOOK_URL env variable not set") };
            }

            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync(nameof(RunApprovalOrchestrator), null);

            log.LogWarning($"Started orchestration with ID = '{instanceId}'.");

            var response = starter.CreateCheckStatusResponse(req, instanceId);

            return response;
        }

        [FunctionName(nameof(HttpRaiseEvent))]
        public static async Task<HttpResponseMessage> HttpRaiseEvent(
            [HttpTrigger(AuthorizationLevel.Anonymous)]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            ILogger log)
        {
            string instanceId = req.RequestUri.ParseQueryString()["instanceId"];
            string eventName = req.RequestUri.ParseQueryString()["event"];

            log.LogInformation($"request event {eventName} for instanceId {instanceId}");

            await starter.RaiseEventAsync(instanceId, eventName);

            return new HttpResponseMessage { Content = new StringContent("OK") };
        }


        [FunctionName(nameof(HttpRaiseEventFromSlack))]
        public static async Task<HttpResponseMessage> HttpRaiseEventFromSlack(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            ILogger log)
        {
            var content = await req.Content.ReadAsStringAsync();

            // see https://api.slack.com/docs/interactive-message-field-guide#option_fields_to_place_within_message_menu_actions
            var payload = JsonConvert.DeserializeObject<SlackActionRequest>(WebUtility.UrlDecode(content.Substring("payload=".Length)));

            log.LogInformation($"Slack action POST request payload: {JsonConvert.SerializeObject(payload)}");
            var buttonInfo = payload.actions[0].name;
            
            var parts = buttonInfo.Split("|");

            log.LogInformation($"raising event {parts[1]} for instance {parts[0]}");
            await starter.RaiseEventAsync(parts[0], parts[1]);

            // we would like to reuse to post back payload.original_message (sans the action buttons)
            // but Slack does provide the picture not as URL but as a fallback name
            // so we can't re-post the pic, but that's essential for the usecase.. so we rebuild the slack message from scratch
            var message = SlackHelper.GetSlackMessage($"Is this picture pornographic?",
                imageUrl: "https://appsfactoryuploads.blob.core.windows.net/public/pics/gerkindick.jpg");

            return new HttpResponseMessage { StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonConvert.SerializeObject(message), Encoding.UTF8, "application/json")
            };
        }
    }
}