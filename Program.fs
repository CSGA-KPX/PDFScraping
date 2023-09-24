open System
open System.Collections.Generic
open System.IO
open System.Text

open AnnotTypes


let markdownFormatter (ret: TextExtractor.ExtractResult) =
    let mutable imageIdx = -1
    let inFile = ret.Path
    let inDir = Path.GetDirectoryName(inFile)

    let getImagePath () =
        imageIdx <- imageIdx + 1
        Path.Join(inDir, $"{Path.GetFileNameWithoutExtension(inFile)}_%02i{imageIdx}.png")

    use outFile =
        let path = Path.Join(inDir, $"{Path.GetFileNameWithoutExtension(inFile)}.md")
        let file = File.Open(path, FileMode.Create)
        new StreamWriter(file, UTF8Encoding(true))

    outFile.WriteLine($"# {ret.Title}")

    if ret.Source.IsSome then
        outFile.WriteLine($"src: `{ret.Source.Value}`")

    outFile.WriteLine("")

    for (page, annots) in ret.Annots do
        outFile.WriteLine($"## Page {page}")

        for annot in annots do
            match annot with
            | Highlight(_, text) -> outFile.WriteLine($"> Highlight: {text}")
            | HighlightNote(_, text, note) -> 
                outFile.WriteLine($"> HighlightNote Text: {text}")
                outFile.WriteLine($"> HighlightNote Note: {note}")
            | Popup(_, text) -> outFile.WriteLine($"> Popup: {text}")
            | StrikeOut(_, text) -> outFile.WriteLine($"> StrikeOut: {text}")
            | Underline(_, text) -> outFile.WriteLine($"> Underline: {text}")
            | FreeText(_, text) -> outFile.WriteLine($"> FreeText: {text}")
            | Image(_, text, bytes) ->
                let imgFile = getImagePath ()
                File.WriteAllBytes(imgFile, bytes)
                let relaPath = Path.GetRelativePath(inDir, imgFile)
                outFile.WriteLine($"> Image: ![{text}]({relaPath})")

            outFile.WriteLine()

[<EntryPoint>]
let main args =
    let files = args |> Array.filter (File.Exists)

    for file in files do
        let ret = TextExtractor.processFile (file)
        markdownFormatter ret

    printfn "OK"
    Console.ReadLine() |> ignore
    0
