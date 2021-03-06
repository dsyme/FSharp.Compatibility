﻿(*

Copyright 2005-2009 Microsoft Corporation
Copyright 2012 Jack Pappas

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

*)

// References:
// http://caml.inria.fr/pub/docs/manual-ocaml/manual037.html
// http://hal.inria.fr/docs/00/07/00/27/PDF/RT-0141.pdf


/// <summary>Operation on arbitrary-precision numbers.</summary>
/// <remarks>Numbers (type num) are arbitrary-precision rational numbers, plus the
/// special elements 1/0 (infinity) and 0/0 (undefined).</remarks>
[<CompilerMessage(
    "This module is for ML compatibility. \
    This message can be disabled using '--nowarn:62' or '#nowarn \"62\"'.",
    62, IsHidden = true)>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module FSharp.Compatibility.OCaml.Num

open System
open System.Globalization
open System.Numerics
//open Microsoft.FSharp.Math
open Ratio

// TEMP : This is needed to force the use of the BigRational type from Math.NET Numerics
// instead of the one from the F# PowerPack.
type BigRational = MathNet.Numerics.BigRational

// TEMP : Alias for 'nat' so it can be used by the function definitions below.
// TODO : For full compatibility, 'nat' needs to be defined as in OCaml, i.e.,
// as an inductive type which can represent an arbitrary-length unsigned integer.
type nat = uint64


//
[<CustomEquality; CustomComparison>]
type Num =
    /// 32-bit signed integer.
    | Int of int
    /// Arbitrary-precision integer.
    | Big_int of bigint
    // Arbitrary-precision rational.
    | Ratio of ratio  // TODO : Change to 'ratio'

    //
    static member Zero
        with get () = Int 0

    //
    static member One
        with get () = Int 1

    //
    static member (*inline*) private FromInt64 (value : int64) : Num =
        if value > (int64 Int32.MaxValue) ||
            value < (int64 Int32.MinValue) then
            Big_int <| bigint value
        else
            Int <| int value

    //
    static member (*inline*) private FromBigInt (value : bigint) : Num =
        // OPTIMIZE : Create static (let-bound) values to hold bigint versions
        // of Int32.MinValue and Int32.MaxValue
        if value > (bigint Int32.MaxValue) ||
            value < (bigint Int32.MinValue) then
            Big_int value
        else
            Int <| int value

    //
    static member (*inline*) private FromBigRational (value : BigRational) =
        // Determine if the BigRational represents a whole (i.e., non-fractional)
        // quantity; if so, convert it to an int or bigint.
        if (value.Numerator % value.Denominator).IsZero then
            value.Numerator / value.Denominator
            |> Num.FromBigInt
        else
            Ratio value

    static member op_Addition (x : Num, y : Num) : Num =
        match x, y with
        | Int x, Int y ->
            (int64 x) + (int64 y)
            |> Num.FromInt64
        | Int x, Big_int y ->
            (bigint x) + y
            |> Num.FromBigInt
        | Int x, Ratio y ->
            Ratio <| (BigRational.FromInt x) + y
        | Big_int x, Int y ->
            x + (bigint y)
            |> Num.FromBigInt
        | Big_int x, Big_int y ->
            x + y
            |> Num.FromBigInt
        | Big_int x, Ratio y ->
            (BigRational.FromBigInt x) + y
            |> Ratio
        | Ratio x, Int y ->
            x + (BigRational.FromInt y)
            |> Ratio
        | Ratio x, Big_int y ->
            x + (BigRational.FromBigInt y)
            |> Ratio
        | Ratio x, Ratio y ->
            x + y
            |> Num.FromBigRational

    static member op_Subtraction (x : Num, y : Num) : Num =
        match x, y with
        | Int x, Int y ->
            (int64 x) - (int64 y)
            |> Num.FromInt64
        | Int x, Big_int y ->
            (bigint x) - y
            |> Num.FromBigInt
        | Int x, Ratio y ->
            Ratio <| (BigRational.FromInt x) - y
        | Big_int x, Int y ->
            x - (bigint y)
            |> Num.FromBigInt
        | Big_int x, Big_int y ->
            x - y
            |> Num.FromBigInt
        | Big_int x, Ratio y ->
            (BigRational.FromBigInt x) - y
            |> Ratio
        | Ratio x, Int y ->
            x - (BigRational.FromInt y)
            |> Ratio
        | Ratio x, Big_int y ->
            x - (BigRational.FromBigInt y)
            |> Ratio
        | Ratio x, Ratio y ->
            x - y
            |> Num.FromBigRational

    static member op_Multiply (x : Num, y : Num) : Num =
        match x, y with
        | Int x, Int y ->
            (int64 x) * (int64 y)
            |> Num.FromInt64
        | Int x, Big_int y ->
            (bigint x) * y
            |> Big_int
        | Int x, Ratio y ->
            (BigRational.FromInt x) * y
            |> Num.FromBigRational
        | Big_int x, Int y ->
            x * (bigint y)
            |> Big_int
        | Big_int x, Big_int y ->
            x * y
            |> Big_int
        | Big_int x, Ratio y ->
            (BigRational.FromBigInt x) * y
            |> Num.FromBigRational
        | Ratio x, Int y ->
            x * (BigRational.FromInt y)
            |> Num.FromBigRational
        | Ratio x, Big_int y ->
            x * (BigRational.FromBigInt y)
            |> Num.FromBigRational
        | Ratio x, Ratio y ->
            x * y
            |> Num.FromBigRational

    static member op_Division (x : Num, y : Num) : Num =
        // Preconditions
        if y.IsZero then
            Exception ("Division_by_zero",
                DivideByZeroException ())
            |> raise

        (*  Don't perform the actual division operation -- just create a Ratio
            from the inputs to avoid any possible truncation of the result. *)
        let x, y =
            match x, y with
            | Int x, Int y ->
                (BigRational.FromInt x), (BigRational.FromInt y)
            | Int x, Big_int y ->
                (BigRational.FromInt x), (BigRational.FromBigInt y)
            | Int x, Ratio y ->
                (BigRational.FromInt x), y
            | Big_int x, Int y ->
                (BigRational.FromBigInt x), (BigRational.FromInt y)
            | Big_int x, Big_int y ->
                (BigRational.FromBigInt x), (BigRational.FromBigInt y)
            | Big_int x, Ratio y ->
                (BigRational.FromBigInt x), y
            | Ratio x, Int y ->
                x, (BigRational.FromInt y)
            | Ratio x, Big_int y ->
                x, (BigRational.FromBigInt y)
            | Ratio x, Ratio y ->
                x, y

        // Divide the values and create a Ratio from the result.
        // Attempt to reduce the result before returning it.
        Num.FromBigRational (x / y)

    //
    static member Quotient (x : Num, y : Num) : Num =
        match x, y with
        (*  Check for division by zero. *)
        | _, y when y.IsZero ->
            Exception ("Division_by_zero",
                DivideByZeroException ())
            |> raise

        (* Standard cases *)
        | Int x, Int y ->
            Int (x / y)
        | Int x, Big_int y ->
            (bigint x) / y
            |> Num.FromBigInt
        | Big_int x, Int y ->
            x / (bigint y)
            |> Num.FromBigInt
        | Big_int x, Big_int y ->
            x / y
            |> Num.FromBigInt
        | Int _, Ratio _
        | Big_int _, Ratio _
        | Ratio _, Int _
        | Ratio _, Big_int _
        | Ratio _, Ratio _ ->
            Num.Floor (x / y)

    static member op_Modulus (x : Num, y : Num) : Num =
        match x, y with
        (* Check for division-by-zero. *)
        | _, y when y.IsZero ->
            Exception ("Division_by_zero",
                DivideByZeroException ())
            |> raise

        | Int x, Int y ->
            Int (x % y)
        | Int x, Big_int y ->
            (bigint x) % y
            |> Num.FromBigInt
        | Big_int x, Int y ->
            x % (bigint y)
            |> Num.FromBigInt
        | Big_int x, Big_int y ->
            x % y
            |> Num.FromBigInt
        | Int _, Ratio _
        | Big_int _, Ratio _
        | Ratio _, Int _
        | Ratio _, Big_int _
        | Ratio _, Ratio _ ->
            x - (y * Num.Quotient (x, y))

    static member op_UnaryNegation (x : Num) : Num =
        match x with
        | Int x ->
            // Handle Int32.MinValue correctly by changing it to a bigint.
            if x = Int32.MinValue then
                Big_int <| -(BigInteger Int32.MinValue)
            else
                Int -x
        | Big_int x ->
            Big_int -x
        | Ratio x ->
            Ratio -x

    //
    static member Abs (x : Num) : Num =
        match x with
        | Int x ->
            // Need to handle Int32.MinValue correctly by changing it to a bigint.
            if x = System.Int32.MinValue then
                BigInteger Int32.MinValue
                |> BigInteger.Abs
                |> Big_int
            else
                Int <| abs x
        | Big_int x ->
            BigInteger.Abs x
            |> Big_int
        | Ratio x ->
            BigRational.Abs x
            |> Ratio

    //
    static member Max (x : Num, y : Num) =
        match x, y with
        | Int a, Int b ->
            Int <| max a b
        | Big_int a, Big_int b ->
            Big_int <| max a b
        | Ratio a, Ratio b ->
            Ratio <| max a b

        | ((Int a) as x), ((Big_int b) as y)
        | ((Big_int b) as y), ((Int a) as x) ->
            if (bigint a) > b then x else y

        | ((Int a) as x), ((Ratio b) as y)
        | ((Ratio b) as y), ((Int a) as x) ->
            if (BigRational.FromInt a) > b then x else y

        | ((Big_int a) as x), ((Ratio b) as y)
        | ((Ratio b) as y), ((Big_int a) as x) ->
            if (BigRational.FromBigInt a) > b then x else y

    //
    static member Min (x : Num, y : Num) =
        match x, y with
        | Int a, Int b ->
            Int <| min a b
        | Big_int a, Big_int b ->
            Big_int <| min a b
        | Ratio a, Ratio b ->
            Ratio <| min a b

        | ((Int a) as x), ((Big_int b) as y)
        | ((Big_int b) as y), ((Int a) as x) ->
            if (bigint a) < b then x else y

        | ((Int a) as x), ((Ratio b) as y)
        | ((Ratio b) as y), ((Int a) as x) ->
            if (BigRational.FromInt a) < b then x else y

        | ((Big_int a) as x), ((Ratio b) as y)
        | ((Ratio b) as y), ((Big_int a) as x) ->
            if (BigRational.FromBigInt a) < b then x else y

    //
    static member Pow (x : Num, y : Num) : Num =
        match y with
        | Int y ->
            Num.Pow (x, y)
        | Big_int y ->
            // TODO : Implement this case -- it works in the original OCaml module.
            raise <| System.NotImplementedException "Num.Pow (Num, Num)"
        | Ratio _ ->
            // TODO : This could actually be implemented, if it would be useful.
            raise <| System.NotSupportedException "Cannot raise a Num to a fractional (Ratio) power."

    //
    static member Pow (x : Num, y : int) : Num =
        match x with
        | Int x ->
            BigInteger.Pow (bigint x, y)
            |> Num.FromBigInt
        | Big_int x ->
            BigInteger.Pow (x, y)
            |> Num.FromBigInt
        | Ratio q ->
            BigRational.PowN (q, y)
            |> Num.FromBigRational

    //
    static member Sign (x : Num) : int =
        match x with
        | Int x ->
            Math.Sign x
        | Big_int x ->
            x.Sign
        | Ratio x ->
            x.Sign

    //
    static member Ceiling (x : Num) : Num =
        match x with
        | Int _
        | Big_int _ as x -> x
        | Ratio q ->
            if (q.Numerator % q.Denominator).IsZero then x
            else
                (q.Numerator / q.Denominator) + BigInteger.One
                |> Num.FromBigInt

    //
    static member Floor (x : Num) : Num =
        match x with
        | Int _
        | Big_int _ as x -> x
        | Ratio q ->
            q.Numerator / q.Denominator
            |> Num.FromBigInt

    //
    static member Round (x : Num) : Num =
        match x with
        | Int _
        | Big_int _ as x -> x
        | Ratio q ->
            // Round to nearest integer (i.e., 1/3 rounds to 0 and 2/3 rounds to 1).
            // NOTE : 1/2 rounds to 1.
            raise <| System.NotImplementedException "Num.Round"

    //
    static member Truncate (x : Num) : Num =
        match x with
        | Int _
        | Big_int _ as x -> x
        | Ratio q ->
            // Truncate any fractional part of the value -- i.e., return a bigint
            // containing the integer part of the Ratio.            
            raise <| System.NotImplementedException "Num.Truncate"

    //
    static member Parse (str : string) : Num =
        // Preconditions
        if str = null then
            raise <| ArgumentNullException "str"
        elif String.length str < 1 then
            ArgumentException ("The string is empty.", "str")
            |> raise

        match BigInteger.TryParse str with
        | true, value ->
            Num.FromBigInt value
        | false, _ ->
            // Try parsing the string as a rational.
            BigRational.Parse str
            |> Num.FromBigRational

    override this.ToString () =
        match this with
        | Int x ->
            x.ToString ()
        | Big_int x ->
            x.ToString ()
        | Ratio q ->
            q.ToString ()

    //
    member this.IsZero
        with get () =
            match this with
            | Int x ->
                x = 0
            | Big_int x ->
                x.IsZero
            | Ratio q ->
                q.Numerator.IsZero

    //
    static member private AreEqual (x : Num, y : Num) : bool =
        match x, y with
        | Int a, Int b ->
            a = b
        | Big_int a, Big_int b ->
            a = b
        | Ratio a, Ratio b ->
            a = b

        | Int a, Big_int b
        | Big_int b, Int a ->
            (bigint a) = b

        | Int a, Ratio b
        | Ratio b, Int a ->
            (BigRational.FromInt a) = b

        | Big_int a, Ratio b
        | Ratio b, Big_int a ->
            (BigRational.FromBigInt a) = b

    static member private Compare (x : Num, y : Num) : int =
        match x, y with
        | Int a, Int b ->
            compare a b
        | Big_int a, Big_int b ->
            compare a b
        | Ratio a, Ratio b ->
            compare a b

        | Int a, Big_int b
        | Big_int b, Int a ->
            compare (bigint a) b

        | Int a, Ratio b
        | Ratio b, Int a ->
            compare (BigRational.FromInt a) b

        | Big_int a, Ratio b
        | Ratio b, Big_int a ->
            compare (BigRational.FromBigInt a) b

    override this.Equals (other : obj) =
        match other with
        | :? Num as other ->
            Num.AreEqual (this, other)
        | _ ->
            false

    override this.GetHashCode () =
        match this with
        | Int x -> x
        | Big_int x ->
            x.GetHashCode ()
        | Ratio x ->
            x.GetHashCode ()

    interface System.IEquatable<Num> with
        //
        member this.Equals (other : Num) =
            Num.AreEqual (this, other)

    interface System.IComparable with
        //
        member this.CompareTo (other : obj) =
            match other with
            | :? Num as other ->
                Num.Compare (this, other)
            | _ ->
                // Should we throw an exception or return 1 or -1?
                raise <| System.NotSupportedException ()

    interface System.IComparable<Num> with
        //
        member this.CompareTo (other : Num) =
            Num.Compare (this, other)


/// Type alias for Num, for compatibility with OCaml.
type num = Num

(* TODO :   Add [<CompilerMessage>] to the functions below so when they're used
            the F# compiler will emit a warning to let the user know they can
            use the equivalent, built-in F# generic function.
            E.g., use the generic 'abs' instead of 'abs_num'. *)

/// Addition.
let inline add_num (x : num) (y : num) : num =
    x + y

let inline ( +/ ) (x : num) (y : num) : num =
    x + y

/// Unary negation.
let inline minus_num (x : num) : num =
    -x

let inline ( -/ ) (x : num) (y : num) : num =
    x - y

/// Subtraction.
let inline sub_num (x : num) (y : num) : num =
    x - y

let inline ( */ ) (x : num) (y : num) : num =
    x * y

/// Multiplication.
let inline mult_num (x : num) (y : num) : num =
    x * y

/// Square the number.
let inline square_num (x : num) : num =
    x * x

/// Division.
let inline div_num (x : num) (y : num) : num =
    x / y

/// Euclidian division.
let inline quo_num (x : num) (y : num) : num =
    Num.Quotient (x, y)

/// Modulus division.
let inline mod_num (x : num) (y : num) : num =
    x % y

//
let inline ( **/ ) (x : num) (y : num) : num =
    num.Pow (x, y)

/// Raise a number to an exponent.
let inline power_num (x : num) (y : num) : num =
    num.Pow (x, y)

/// Absolute value.
let inline abs_num (x : num) : num =
    num.Abs x

//
let inline succ_num (n : num) : num =
    n + (Int 1)

//
let inline pred_num (n : num) : num =
    n - (Int 1)

//
let incr_num (r : num ref) : unit =
    r := succ_num !r

//
let decr_num (r : num ref) : unit =
    r := pred_num !r

/// Test if a number is an integer.
let is_integer_num (n : num) : bool =
    match n with
    | Int _
    | Big_int _ ->
        true
    | Ratio q ->
        (q.Numerator % q.Denominator).IsZero


(* The four following functions approximate a number by an integer *)

//
let inline integer_num (n : num) : num =
    Num.Truncate n

//
let inline floor_num (n : num) : num =
    num.Floor n

//
let inline round_num (n : num) : num =
    Num.Round n

//
let inline ceiling_num (n : num) : num =
    num.Ceiling n

//
let inline sign_num (n : num) : int =
    num.Sign n


(* Comparisons between numbers *)

let inline ( =/ ) (x : num) (y : num) =
    x = y
let inline ( </ ) (x : num) (y : num) =
    x < y
let inline ( >/ ) (x : num) (y : num) =
    x > y
let inline ( <=/ ) (x : num) (y : num) =
    x <= y
let inline ( >=/ ) (x : num) (y : num) =
    x >= y
let inline ( <>/ ) (x : num) (y : num) =
    x <> y
let inline eq_num (x : num) (y : num) =
    x = y
let inline lt_num (x : num) (y : num) =
    x < y
let inline le_num (x : num) (y : num) =
    x <= y
let inline gt_num (x : num) (y : num) =
    x > y
let inline ge_num (x : num) (y : num) =
    x >= y

/// Return -1, 0 or 1 if the first argument is less than, equal to, or greater than the second argument.
let inline compare_num (x : num) (y : num) =
    compare x y
/// Return the greater of the two arguments.
let inline max_num (x : num) (y : num) =
    num.Max (x, y)
/// Return the smaller of the two arguments.
let inline min_num (x : num) (y : num) =
    num.Min (x, y)


(* Coercions with strings *)

//
let inline string_of_num (n : num) : string =
    n.ToString ()

//
let approx_num_fix (precision : int) (n : num) : string =
    raise <| System.NotImplementedException "approx_num_fix"

//
let approx_num_exp (precision : int) (n : num) : string =
    raise <| System.NotImplementedException "approx_num_exp"

/// Convert a string to a number.
/// Raise Failure "num_of_string" if the given string is not a valid representation of an integer
let num_of_string (str : string) : num =
    // If the string can't be parsed (i.e., an exception was thrown),
    // catch the exception then raise an OCaml-compatible exception.
    try
        num.Parse str
    with ex ->
        Exception ("num_of_string", ex)
        |> raise

(* Coercions between numerical types *)

//
let int_of_num (n : num) : int =
    match n with
    | Int x -> x
    | Big_int x ->
        // TODO : If 'n' is too large to fit into an 'int', then fail with
        // the message "int_of_string" for compatbility with OCaml.
        raise <| System.NotImplementedException "int_of_num"
    | Ratio q ->
        // TODO : If 'q' can not be represented as an 'int', then fail with
        // the message "int_of_string" for compatbility with OCaml.
        raise <| System.NotImplementedException "int_of_num"

//
let inline num_of_int (r : int) : num =
    Int r

//
let nat_of_num (n : num) : nat =
    // TODO : Determine how to handle cases where 'n' is a Ratio or
    // is a Big_int whose value is too large for an 'int'.
    raise <| System.NotImplementedException "nat_of_num"

//
let num_of_nat (r : nat) : num =
    raise <| System.NotImplementedException "num_of_nat"

//
let inline num_of_big_int (i : bigint) : num =
    Big_int i

//
let big_int_of_num (n : num) : bigint =
    match n with
    | Int i ->
        bigint i
    | Big_int i ->
        i
    | Ratio q ->
        raise <| System.NotImplementedException "big_int_of_num"

//
let ratio_of_num (n : num) : BigRational =
    match n with
    | Int i ->
        BigRational.FromInt i
    | Big_int i ->
        BigRational.FromBigInt i
    | Ratio q ->
        q

//
let inline num_of_ratio (q : BigRational) : num =
    Ratio q

//
let float_of_num (n : num) : float =
    raise <| System.NotImplementedException "float_of_num"

