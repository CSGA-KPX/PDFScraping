module TextExtractor

open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Text.RegularExpressions

open UglyToad.PdfPig
open UglyToad.PdfPig.Geometry
open UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor

open AnnotTypes


type ExtractResult =
    { Path: string
      Title: string
      Source: string option
      Annots: (int * AnnotTypes.Annot[])[] }

let private removeSpace = Regex(@" +", RegexOptions.Compiled)

let private formatText (str: string) =
    let str = str.Normalize(NormalizationForm.FormKD)
    removeSpace.Replace(str, " ")

let private readArea (annot: Annotations.Annotation) (words: seq<Content.Word>) =
    let rects =
        [| for point in annot.QuadPoints do
               let minX = (point.Points |> Seq.minBy (fun x -> x.X)).X
               let minY = (point.Points |> Seq.minBy (fun x -> x.Y)).Y
               let maxX = (point.Points |> Seq.maxBy (fun x -> x.X)).X
               let maxY = (point.Points |> Seq.maxBy (fun x -> x.Y)).Y

               Core.PdfRectangle(Core.PdfPoint(minX, minY), Core.PdfPoint(maxX, maxY)) |]

    let filtered =
        words
        |> Seq.filter (fun word -> rects |> Array.exists (fun rect -> rect.IntersectsWith(word.BoundingBox)))
        // Normalize to remove f? i? ligatures
        // TrimEnd to remove hyphen
        // Trim whitespaces
        |> Seq.map (fun word -> word.Text.TrimEnd('-'))

    String.Join(' ', filtered) |> formatText

let processFile (file: string) =
    use render = new PageRender.PageRenderCache(file)
    use doc = PdfDocument.Open(file)

    let items = ResizeArray<Annot>()

    for page in doc.GetPages() do
        let words = page.GetWords(NearestNeighbourWordExtractor.Instance)

        for annot in page.ExperimentalAccess.GetAnnotations() do
            match annot.Type with
            | Annotations.AnnotationType.Link -> ()
            | Annotations.AnnotationType.Highlight ->
                let text = readArea annot words

                if String.IsNullOrWhiteSpace(annot.Content) then
                    items.Add(Highlight(page.Number, text))
                else
                    items.Add(HighlightNote(page.Number, text, annot.Content))

            | Annotations.AnnotationType.FreeText ->
                if not <| String.IsNullOrWhiteSpace(annot.Content) then
                    items.Add(FreeText(page.Number, annot.Content))

            | Annotations.AnnotationType.Popup ->
                if not <| String.IsNullOrWhiteSpace(annot.Content) then
                    items.Add(Popup(page.Number, annot.Content))

            | Annotations.AnnotationType.StrikeOut ->
                let text = readArea annot words

                if not <| String.IsNullOrWhiteSpace(text) then
                    items.Add(StrikeOut(page.Number, text))

            | Annotations.AnnotationType.Underline ->
                let text = readArea annot words

                if not <| String.IsNullOrWhiteSpace(text) then
                    items.Add(Underline(page.Number, text))

            | Annotations.AnnotationType.Square ->
                let bmp = render.CropPage(page.Number, annot.Rectangle)
                let text = if isNull annot.Content then String.Empty else annot.Content
                items.Add(Square(page.Number, annot.Content, bmp))
            | t -> printfn $"unknwon {t} -> {annot.Content}"


    let metaData = PdfMetadata.processMetadata (doc)

    let source =
        if metaData.Doi.IsNone then
            if metaData.Title.IsSome then Some file else None
        else
            metaData.Doi

    { ExtractResult.Path = file
      ExtractResult.Annots = items.ToArray() |> Array.groupBy (fun item -> item.Page)
      ExtractResult.Source = source
      ExtractResult.Title = metaData.Title |> Option.defaultValue file }
