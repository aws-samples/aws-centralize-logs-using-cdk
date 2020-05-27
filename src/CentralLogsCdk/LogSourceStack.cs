using Amazon.CDK;
using Amazon.CDK.AWS.Logs;
using System;

namespace CentralLogsCdk
{
    internal class LogSourceStack : Stack
    {
       private string _logDestinationArn { get; set; }
       private string _logGroupName { get; set; }

        internal LogSourceStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            var logGroupName = new CfnParameter(this, "LogGroupName", new CfnParameterProps
            {
                Type = "String",
                Description = "The name of the CloudWatch Log Group Name to apply the subscription."
            });
            if (!string.IsNullOrEmpty(logGroupName.ValueAsString))
            {Console.WriteLine("LogDestinationArn: ----------- " + logGroupName.ValueAsString);}     
            _logGroupName = logGroupName.ValueAsString;

            var logDestinationArn = new CfnParameter(this, "LogDestinationArn", new CfnParameterProps
            {
                Type = "String",
                Description = "The ARN of the LogDestination."
            });
            if (!string.IsNullOrEmpty(logDestinationArn.ValueAsString))
            {Console.WriteLine("LogGroupName: ----------- " + logDestinationArn.ValueAsString);}     
            _logDestinationArn = logDestinationArn.ValueAsString;

            AddLogSubscription();
        }

        public void AddLogSubscription()
        {
            var cfnSubscriptionFilter = new CfnSubscriptionFilter(this, "SubscriptionFilter", new CfnSubscriptionFilterProps{
                DestinationArn = _logDestinationArn,
                FilterPattern = "",
                LogGroupName = _logGroupName
            });            
        }
    }
}