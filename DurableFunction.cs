using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

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
                        Type = "button",
                        Name = "travel_request_123456",
                        Text = "Reject",
                        Style = "danger",
                        Url = $"https://{host}/api/HttpRaiseEvent?instanceid={context.InstanceId}&event={RejectedEventName}",
                    },
                    new SlackMessageAttachmentAction {
                        Type = "button",
                        Name = "travel_request_321",
                        Text = "Approve",
                        Style = "primary",
                        Url = $"https://{host}/api/HttpRaiseEvent?instanceid={context.InstanceId}&event={ApprovalEventName}",
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
            if(slackHookUrl == null)
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
    }
}