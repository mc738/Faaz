namespace Faaz.V1.FSharp.Compiled

open System
open System.Linq.Expressions
open System.Reflection

module Extractor =

    module Internal =

        let defaultBindingFlags = BindingFlags.Static ||| BindingFlags.Public

        let getMemberInfo (name: string) (assembly: Assembly) (bindingFlags: BindingFlags) =
            let fqTypeName, memberName =
                let splitIndex = name.LastIndexOf(".")

                name.[0 .. splitIndex - 1], name.[splitIndex + 1 ..]

            let candidates =
                assembly.GetTypes()
                |> Seq.where (fun t -> t.FullName = fqTypeName)
                |> Seq.toList

            match candidates with
            | [ t ] ->
                match t.GetMethod(memberName, bindingFlags) with
                | null -> Error ""
                | memberInfo -> Ok memberInfo
            | [] -> Error ""
            | _ -> Error ""

        let extractor<'r> name assembly parameters =
            match getMemberInfo name assembly defaultBindingFlags with
            | Ok memberInfo ->
                let lambda =
                    let expression =
                        if (typeof<'r> = typeof<unit>) then
                            Expression.Block(
                                Expression.Call(memberInfo, parameters |> Array.map (fun param -> param :> Expression)),
                                Expression.Constant((), typeof<'r>)
                            )
                            :> Expression
                        else
                            Expression.Convert(
                                Expression.Call(memberInfo, parameters |> Array.map (fun param -> param :> Expression)),
                                typeof<'r>
                            )
                            :> Expression

                    Expression.Lambda (expression, parameters)

                let systemFunc = lambda.Compile()

                systemFunc |> Ok

            | Error e -> Error ""

        let extractFunction1<'T1, 'TResult> name (assembly: Assembly) : Result<'T1 -> 'TResult, string> =
            [| Expression.Parameter(typeof<'T1>) |]
            |> extractor<'TResult> name assembly
            |> Result.map (fun systemFunc -> systemFunc :?> Func<'T1, 'TResult> |> FuncConvert.FromFunc)

        let extractFunction2<'T1, 'T2, 'TResult> name (assembly: Assembly) : Result<'T1 -> 'T2 -> 'TResult, string> =
            [| Expression.Parameter(typeof<'T1>); Expression.Parameter(typeof<'T2>) |]
            |> extractor<'TResult> name assembly
            |> Result.map (fun systemFunc -> systemFunc :?> Func<'T1, 'T2, 'TResult> |> FuncConvert.FromFunc)

        let extractFunction3<'T1, 'T2, 'T3, 'TResult> name (assembly: Assembly) =
            [| Expression.Parameter(typeof<'T1>)
               Expression.Parameter(typeof<'T2>)
               Expression.Parameter(typeof<'T3>) |]
            |> extractor<'TResult> name assembly
            |> Result.map (fun systemFunc -> systemFunc :?> Func<'T1, 'T2, 'T3, 'TResult> |> FuncConvert.FromFunc)

        let extractFunction4<'T1, 'T2, 'T3, 'T4, 'TResult> name (assembly: Assembly) =
            [| Expression.Parameter(typeof<'T1>)
               Expression.Parameter(typeof<'T2>)
               Expression.Parameter(typeof<'T3>)
               Expression.Parameter(typeof<'T4>) |]
            |> extractor<'TResult> name assembly
            |> Result.map (fun systemFunc -> systemFunc :?> Func<'T1, 'T2, 'T3, 'T4, 'TResult> |> FuncConvert.FromFunc)


    type FunctionExtractor(assembly: Assembly) =

        member _.TryGetFunction<'T1, 'TResult>(name) =
            Internal.extractFunction1<'T1, 'TResult> name assembly

        member _.TryGetFunction<'T1, 'T2, 'TResult>(name) =
            Internal.extractFunction2<'T1, 'T2, 'TResult> name assembly

        member _.TryGetFunction<'T1, 'T2,'T3, 'TResult>(name) =
            Internal.extractFunction3<'T1, 'T2,'T3, 'TResult> name assembly

        member _.TryGetFunction<'T1, 'T2, 'T3, 'T4, 'TResult>(name) =
            Internal.extractFunction4<'T1, 'T2, 'T3, 'T4, 'TResult> name assembly

       
        
