module PdfMetadata

open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Text.RegularExpressions
open System.Xml.Linq

open UglyToad.PdfPig


type XMPResult =
    { Title: string option
      Doi: string option }

let private titleRegex =
    Regex("title(\[\d\])?$", RegexOptions.IgnoreCase ||| RegexOptions.Compiled)

let private doiRegex =
    Regex("(doi:)? ?10\.\d\d\d\d", RegexOptions.IgnoreCase ||| RegexOptions.Compiled)

type RDFProp =
    | Single of string
    | Bag of string[]
    | Seq of string[]
    | Alt of string[]

    member x.GetValue() =
        // https://stackoverflow.com/questions/29001433/how-rdfbag-rdfseq-and-rdfalt-is-different-while-using-them
        match x with
        | Single str -> str
        | Bag strs -> String.Join(' ', strs)
        | Seq strs -> String.Join(' ', strs)
        | Alt strs -> strs |> Array.tryHead |> Option.defaultValue String.Empty

    static member FromValues(tag, values) =
        match tag with
        | "Bag" -> Bag values
        | "Seq" -> Seq values
        | "Alt" -> Alt values
        | tag -> invalidArg "tag" $"RDF tag >{tag}< invalid"

module RDFProp =
    let Keys = [ "Bag"; "Seq"; "Alt" ] |> set

let processMetadata (doc: PdfDocument) =
    let xmpData =
        let props = ResizeArray<string * RDFProp>()
        let succ, xmp = doc.TryGetXmpMetadata()

        if succ then
            try
                let rec yieldProp (element: XElement) (stack: string) =
                    let name = element.Name.LocalName

                    if not element.HasElements then
                        let value = element.Value
                        props.Add($"{stack}.{name}", Single value)
                    elif RDFProp.Keys.Contains(name) then
                        let values = element.Elements() |> Seq.map (fun e -> e.Value) |> Seq.toArray
                        props.Add(stack, RDFProp.FromValues(name, values))
                    else
                        let stack = $"{stack}.{name}"

                        for child in element.Elements() do
                            yieldProp child stack

                yieldProp (xmp.GetXDocument().Root) ""
            with
            | e -> printfn $"Extract XMP failed : {e.Message}"
        props

    let titleSource =
        [ doc.Information.Title

          for (k, v) in xmpData do
              if k.EndsWith("title", StringComparison.InvariantCultureIgnoreCase) then
                  v.GetValue() ]
        |> List.filter (String.IsNullOrWhiteSpace >> not)
        |> List.sortByDescending (fun str -> str.Length)
        |> List.tryHead

    let doiSource =
        [ let def = Tokens.StringToken("")

          let get str =
              let d = doc.Information.DocumentInformationDictionary.Data
              let t = d.GetValueOrDefault(str, def) :?> Tokens.StringToken
              t.Data

          get "doi"
          get "Doi"
          get "DOI"

          for (_, v) in xmpData do
              let v = v.GetValue()

              if doiRegex.IsMatch(v) then
                  v ]
        |> List.filter (String.IsNullOrWhiteSpace >> not)
        |> List.sortByDescending (fun str -> str.Length)
        |> List.tryHead

    { XMPResult.Title = titleSource
      XMPResult.Doi = doiSource }
