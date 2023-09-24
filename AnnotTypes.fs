module AnnotTypes


type Annot =
    | Highlight of page: int * text: string
    | HighlightNote of page: int * text: string * note : string
    | Popup of page: int * text: string
    | StrikeOut of page: int * text: string
    | Underline of page: int * text: string
    | FreeText of page: int * text: string
    | Image of page: int * text: string * image: byte []

    member x.Page =
        match x with
        | Highlight(page, _) -> page
        | HighlightNote(page, _, _) -> page
        | Popup(page, _) -> page
        | StrikeOut(page, _) -> page
        | Underline(page, _) -> page
        | FreeText(page, _) -> page
        | Image(page, _, _) -> page