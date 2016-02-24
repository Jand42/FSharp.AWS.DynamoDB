﻿namespace FSharp.DynamoDB.Tests

open System
open System.Threading

open Xunit
open FsUnit.Xunit

open FSharp.DynamoDB

[<AutoOpen>]
module SimpleTableTypes =

    type SimpleRecord =
        {
            [<HashKey>]
            HashKey : string
            [<RangeKey>]
            RangeKey : string

            Value : int64

            Tuple : int64 * int64

            Map : Map<string, int64>

            Unions : Choice<string, int64, byte[]> list
        }

    [<HashKeyConstant("HashKey", "compatible")>]
    type CompatibleRecord =
        {
            [<RangeKey; CustomName("RangeKey")>]
            Id : string

            Values : Set<int>
        }

type ``Simple Table Operation Tests`` () =

    let client = getDynamoDBAccount()
    let tableName = getRandomTableName()

    let rand = let r = Random() in fun () -> int64 <| r.Next()
    let mkItem() = 
        { 
            HashKey = guid() ; RangeKey = guid() ; 
            Value = rand() ; Tuple = rand(), rand() ;
            Map = seq { for i in 0L .. rand() % 5L -> "K" + guid(), rand() } |> Map.ofSeq 
            Unions = [Choice1Of3 (guid()) ; Choice2Of3(rand()) ; Choice3Of3(Guid.NewGuid().ToByteArray())]
        }

    let run = Async.RunSynchronously

    let table = TableContext.GetTableContext<SimpleRecord>(client, tableName, createIfNotExists = true) |> run

    [<Fact>]
    let ``Convert to compatible table`` () =
        let table' = table.WithRecordType<CompatibleRecord> ()
        table'.KeySchema |> should equal table.KeySchema

    [<Fact>]
    let ``Simple Put Operation`` () =
        let value = mkItem()
        let key = table.PutItemAsync value |> run
        let value' = table.GetItemAsync key |> run
        value' |> should equal value

    [<Fact>]
    let ``ContainsKey Operation`` () =
        let value = mkItem()
        let key = table.PutItemAsync value |> run
        table.ContainsKeyAsync key |> run |> should equal true

    [<Fact>]
    let ``Batch Put Operation`` () =
        let values = set [ for i in 1L .. 20L -> mkItem() ]
        let keys = table.BatchPutItemsAsync values |> run
        let values' = table.BatchGetItemsAsync keys |> run |> Set.ofArray
        values' |> should equal values

    [<Fact>]
    let ``Simple Delete Operation`` () =
        let item = mkItem()
        let key = table.PutItemAsync item |> run
        table.ContainsKeyAsync key |> run |> should equal true
        table.DeleteItemAsync key |> run
        table.ContainsKeyAsync key |> run |> should equal false

    interface IDisposable with
        member __.Dispose() =
            ignore <| client.DeleteTable(tableName)