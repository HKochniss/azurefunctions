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
    /// <summary>
    /// simple timer example
    /// </summary>
    public static class DurableFunctionTimer
    {

        [FunctionName(nameof(HttpTimerOrchestrationStart))]
        public static async Task<HttpResponseMessage> HttpTimerOrchestrationStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync(nameof(RunTimerOrchestrator), null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            var response = starter.CreateCheckStatusResponse(req, instanceId);

            return response;
        }



        [FunctionName(nameof(RunTimerOrchestrator))]
        public static async Task<List<string>> RunTimerOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context, ILogger log)
        {
            var outputs = new List<string>();

            log.LogWarning("logging on every execution");

            context.SetCustomStatus("first stage");

            // Replace "hello" with the name of your Durable Activity Function.
            outputs.Add(await context.CallActivityAsync<string>(nameof(FunctionSayHello), "Tokyo"));

            context.SetCustomStatus("first stage done");

            if(!context.IsReplaying) log.LogWarning("first stage done");

            // sleep X seconds
            DateTime nextCleanup = context.CurrentUtcDateTime.AddSeconds(5);
            await context.CreateTimer(nextCleanup, CancellationToken.None);

            // call normal activity function, saves result for replay later
            outputs.Add(await context.CallActivityAsync<string>(nameof(FunctionSayHello), "Seattle"));

            context.SetCustomStatus("second stage");

            // sleep X seconds
            nextCleanup = context.CurrentUtcDateTime.AddSeconds(5);
            await context.CreateTimer(nextCleanup, CancellationToken.None);

            context.SetCustomStatus("second stage trying");

            outputs.Add(await context.CallActivityAsync<string>(nameof(FunctionSayHello), "London"));

            context.SetCustomStatus("second stage done");

            // sleep X seconds
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



    }
}