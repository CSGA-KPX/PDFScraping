module PageRender

open System
open System.Drawing
open System.Drawing.Imaging
open System.Collections.Generic

open PDFiumCore


type PageRenderCache(file: string) =
    static do fpdfview.FPDF_InitLibrary()

    static let scale = 2.0f // Scale against 72dpi

    let cache = Dictionary<int, Bitmap>()
    let doc = fpdfview.FPDF_LoadDocument(file, null)

    static member val Dpi = 72.0f * scale

    member x.GetPage(pageStart1: int) =
        let pageStart0 = pageStart1 - 1

        if not <| cache.ContainsKey(pageStart0) then
            cache.[pageStart0] <- x.RenderPage(pageStart0)

        cache.[pageStart0]

    member x.CropPage(pageStart1: int, rect: UglyToad.PdfPig.Core.PdfRectangle) =
        let bmp = x.GetPage(pageStart1)

        let crop =
            let scale = float scale

            Rectangle(
                rect.Left * scale |> int,
                // PdfPig and System.Drawing have different definition on Height
                bmp.Height - (rect.Top * scale |> int),
                rect.Width * scale |> int,
                rect.Height * scale |> int
            )

        bmp.Clone(crop, bmp.PixelFormat)

    member private x.RenderPage(pageStart0: int) =
        let page = fpdfview.FPDF_LoadPage(doc, pageStart0)
        use size = new FS_SIZEF_()
        fpdfview.FPDF_GetPageSizeByIndexF(doc, 0, size) |> ignore

        let width = Math.Round(float <| size.Width * scale) |> float32
        let height = Math.Round(float <| size.Height * scale) |> float32

        let bitmap =
            fpdfview.FPDFBitmapCreateEx(
                int width,
                int height,
                4, // BGRA
                IntPtr.Zero,
                0
            )

        fpdfview.FPDFBitmapFillRect(bitmap, 0, 0, int width, int height, Color.White.ToArgb() |> uint)
        use matrix = new FS_MATRIX_(A = scale, B = 0f, C = 0f, D = scale, E = 0f, F = 0f)
        use clipping = new FS_RECTF_(Left = 0f, Right = width, Bottom = 0f, Top = height)

        fpdfview.FPDF_RenderPageBitmapWithMatrix(bitmap, page, matrix, clipping, 0)

        new Bitmap(
            int width,
            int height,
            fpdfview.FPDFBitmapGetStride(bitmap),
            PixelFormat.Format32bppArgb,
            fpdfview.FPDFBitmapGetBuffer(bitmap)
        )

    interface IDisposable with
        member x.Dispose() =
            fpdfview.FPDF_CloseDocument(doc)

            for item in cache.Values do
                item.Dispose()
