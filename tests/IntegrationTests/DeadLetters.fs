module Pulsar.Client.IntegrationTests.DeadLetters

open System
open Expecto
open Pulsar.Client.Api
open FSharp.Control.Tasks.V2.ContextInsensitive
open System.Threading.Tasks
open Pulsar.Client.Common
open Serilog
open Pulsar.Client.IntegrationTests.Common

[<Tests>]
let tests =

    let client = getClient()

    let newGuid() = Guid.NewGuid().ToString("N")
    let getTopicName() = "public/default/topic-" + newGuid()
    let getProducerName() = sprintf "dlqProducer-%s" (newGuid())
    let getConsumerName() = sprintf "negativeConsumer-%s" (newGuid())
    let getDlqConsumerName() = sprintf "dlqConsumer-%s" (newGuid())
    let getDeadLettersPolicy() = DeadLettersPolicy(0, sprintf "public/default/topic-%s-DLQ" (newGuid()))

    let subscriptionName = "dlqSubscription"
    let numberOfMessages = 50

    let logTestStart testDescription = Log.Debug(sprintf "Started '%s'" testDescription)
    let logTestEnd testDescription = Log.Debug(sprintf "Finished '%s'" testDescription)

    let buildProducer(producerName, topicName) =
        ProducerBuilder(client)
            .ProducerName(producerName)
            .Topic(topicName)
            .EnableBatching(false)

    let buildConsumer(consumerName, topicName, deadLettersPolicy) =
        ConsumerBuilder(client)
            .ConsumerName(consumerName)
            .Topic(topicName)
            .SubscriptionName(subscriptionName)
            .SubscriptionType(SubscriptionType.Shared)
            .NegativeAckRedeliveryDelay(TimeSpan.FromSeconds(4.0))
            .DeadLettersPolicy(deadLettersPolicy)

    let buildDlqConsumer(consumerName, topicName) =
        ConsumerBuilder(client)
            .ConsumerName(consumerName)
            .Topic(topicName)
            .SubscriptionName(subscriptionName)
            .SubscriptionType(SubscriptionType.Shared)

    let receiveAndAckNegative (consumer: IConsumer) number =

        task {
            for _ in 1..number do
                let! message = consumer.ReceiveAsync()
                do! consumer.NegativeAcknowledge(message.MessageId)
        }

    testList "deadLetters" [
        testAsync "Failed messages stored in a configured dead letter topic" {

            let description = "Failed messages stored in a configured dead letter topic"

            description |> logTestStart

            let producerName = getProducerName()
            let consumerName = getConsumerName()
            let dlqConsumerName = getDlqConsumerName()
            let topicName = getTopicName()
            let policy = getDeadLettersPolicy()

            let! producer = buildProducer(producerName, topicName).CreateAsync() |> Async.AwaitTask

            let! consumer = buildConsumer(consumerName, topicName, policy).SubscribeAsync() |> Async.AwaitTask

            let! dlqConsumer =
                buildDlqConsumer(dlqConsumerName, policy.DeadLetterTopic).SubscribeAsync() |> Async.AwaitTask

            let producerTask =
                Task.Run(fun () ->
                    task {
                        do! produceMessages producer numberOfMessages producerName
                    }:> Task)

            let consumerTask =
                Task.Run(fun () ->
                    task {
                        do! receiveAndAckNegative consumer numberOfMessages
                    }:> Task)

            let dlqConsumerTask =
                Task.Run(fun () ->
                    task {
                        do! consumeMessages dlqConsumer numberOfMessages dlqConsumerName
                    }:> Task)

            let tasks =
                [|
                    producerTask;
                    consumerTask;
                    Task.Delay(TimeSpan.FromSeconds(8.0))
                    dlqConsumerTask
                |]

            do! Task.WhenAll(tasks) |> Async.AwaitTask

            description |> logTestEnd
        }

        testAsync "Failed messages stored in a default dead letter topic" {

            let description = "Failed messages stored in a default dead letter topic"

            description |> logTestStart

            let producerName = getProducerName()
            let consumerName = getConsumerName()
            let dlqConsumerName = getDlqConsumerName()
            let topicName = getTopicName()
            let policy = DeadLettersPolicy(0)

            let! producer = buildProducer(producerName, topicName).CreateAsync() |> Async.AwaitTask

            let! consumer = buildConsumer(consumerName, topicName, policy).SubscribeAsync() |> Async.AwaitTask

            let! dlqConsumer =
                buildDlqConsumer(dlqConsumerName, sprintf "%s-%s-DLQ" topicName subscriptionName)
                    .SubscribeAsync() |> Async.AwaitTask

            let producerTask =
                Task.Run(fun () ->
                    task {
                        do! produceMessages producer numberOfMessages producerName
                    }:> Task)

            let consumerTask =
                Task.Run(fun () ->
                    task {
                        do! receiveAndAckNegative consumer numberOfMessages
                    }:> Task)

            let dlqConsumerTask =
                Task.Run(fun () ->
                    task {
                        do! consumeMessages dlqConsumer numberOfMessages dlqConsumerName
                    }:> Task)

            let tasks =
                [|
                    producerTask;
                    consumerTask;
                    Task.Delay(TimeSpan.FromSeconds(8.0))
                    dlqConsumerTask
                |]

            do! Task.WhenAll(tasks) |> Async.AwaitTask

            description |> logTestEnd
        }

        testAsync "Failed batch stored in a configured default letter topic" {

            let description = "Failed batch stored in a configured dead letter topic"

            description |> logTestStart

            let producerName = getProducerName()
            let consumerName = getConsumerName()
            let dlqConsumerName = getDlqConsumerName()
            let topicName = getTopicName()
            let policy = getDeadLettersPolicy()

            let! producer =
                buildProducer(producerName, topicName)
                    .EnableBatching(true)
                    .BatchingMaxMessages(numberOfMessages)
                    .CreateAsync() |> Async.AwaitTask

            let! consumer = buildConsumer(consumerName, topicName, policy).SubscribeAsync() |> Async.AwaitTask

            let! dlqConsumer =
                buildDlqConsumer(dlqConsumerName, policy.DeadLetterTopic).SubscribeAsync() |> Async.AwaitTask

            let producerTask =
                Task.Run(fun () ->
                    task {
                        do! produceMessages producer numberOfMessages producerName
                    }:> Task)

            let consumerTask =
                Task.Run(fun () ->
                    task {
                        do! receiveAndAckNegative consumer numberOfMessages
                    }:> Task)

            let dlqConsumerTask =
                Task.Run(fun () ->
                    task {
                        do! consumeMessages dlqConsumer numberOfMessages dlqConsumerName
                    }:> Task)

            let tasks =
                [|
                    producerTask;
                    consumerTask;
                    Task.Delay(TimeSpan.FromSeconds(8.0))
                    dlqConsumerTask
                |]

            do! Task.WhenAll(tasks) |> Async.AwaitTask

            description |> logTestEnd
        }
    ]