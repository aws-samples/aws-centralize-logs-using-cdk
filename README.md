# Centralize CloudWatch Logs with CDK

## Solution
This solution mainly involves Amazon Kinesis Data Firehose which provides the ability to process data in real-time allowing critical use cases to be implemented based on it. The centralized logging account will expose a Log Destination endpoint which in turn is connected to a Kinesis Firehose. Kinesis Firehose is configured to push data to Amazon S3 Bucket. We could configure a lambda to un-compress/format the data before it is sent to S3. Also we can utilize Kinesis Firehose configuration to transform the data using AWS Glue before pushing it. Also, Firehose can not only push the data to S3 but also supports other destinations like Amazon Redshift, Amazon Elasticsearch Service and Splunk.

![Alt text](Centralize-Log-Pattern.jpg?raw=true "Centralize Logs with CDK")

**Note: Though the diagram shows two different accounts, this solution can be deployed in one single account as well.**

The AWS Cloud Development Kit (AWS CDK) is an open source software development framework to model and provision your cloud application resources using familiar programming languages. CDK with C# has been used in this solution to create our Infrastructure as Code. 

## Pre-Requisites
* AWS CDK [Version 1.36]
* .Net Core 3.1

## Commands to Deploy the Solution

* `git clone https://github.com/navbalaraman/central-logs-cdk.git`
* `cd central-logs-cdk`
* `dotnet build src`

### **If deploying the solution to a single account:**

**Step 1:** Bootstrap your account to prepare the environment with resources that will be required for the deployment.
* `cdk bootstrap`

**Step 2:**  Deploy the `LogDestinationStack` (Replace *AWS-ACCOUNT-ID* with your AWS Account number)
* `cdk deploy LogDestinationStack --parameters LogDestinationStack:SourceAccountNumber="AWS-ACCOUNT-ID"`

![Alt text](CLIOutput.png?raw=true "Centralize Logs with CDK - CLI Ouput")

**Step 3:**  Deploy the `LogSourceStack` (Replace *LOG-DESTINATION-ARN* with the output value from the previous command, and *CLOUDWATCH-LOGGROUP* with the name of the Log group)
* `cdk deploy LogSourceStack --parameters LogSourceStack:LogGroupName="CLOUDWATCH-LOGGROUP" --parameters LogDestinationArn="LOG-DESTINATION-ARN"`


### **If deploying the solution to separate source and destination account:**

**Step 1:**  Deploy the `LogsDestinationStack` (Replace *SOURCE-AWS-ACCOUNT-ID* with your AWS Account number)
* `cdk bootstrap`
* `cdk deploy LogDestinationStack --parameters LogDestinationStack:SourceAccountNumber="SOURCE-AWS-ACCOUNT-ID"`

![Alt text](CLIOutput.png?raw=true "Centralize Logs with CDK - CLI Ouput")

**Step 2:** Deploy the `LogSourceStack` (Replace *LOG-DESTINATION-ARN* with the output value from the previous command, and *CLOUDWATCH-LOGGROUP* with the name of the Log group)
* `cdk bootstrap`
* `cdk deploy LogSourceStack --parameters LogSourceStack:LogGroupName="CLOUDWATCH-LOGGROUP" --parameters LogDestinationArn="LOG-DESTINATION-ARN"`

