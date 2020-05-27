using System;
using Amazon.CDK;

namespace CentralLogsCdk
{
    sealed class Program
    {
        public static void Main(string[] args)
        {
            var app = new App();

            //Stack to create resources to accept, process and push the logs to S3 bucket.
            var logDestinationStack = new LogDestinationStack(app, "LogDestinationStack", new StackProps{
                Env = new Amazon.CDK.Environment
                {
                    Account = Aws.ACCOUNT_ID
                }
            });

            //Source Stack to apply subsription to the Cloud Watch Log Group.
            var logSourceStack = new LogSourceStack(app, "LogSourceStack", new StackProps{});

            app.Synth();
        }
    }
}
