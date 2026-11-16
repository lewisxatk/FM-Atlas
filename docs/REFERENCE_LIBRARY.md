# FM Atlas — Master Reference Library

**Reference set:** 001  
**Added:** 2026-11-16  
**Status:** Approved direction; detailed screen specifications are in [DESIGN_SPECIFICATION.md](./DESIGN_SPECIFICATION.md).  
**Project:** [FM-Atlas repository](https://github.com/lewisxatk/FM-Atlas)

This library is the source of truth for visual and interaction references. New screenshots must be assigned an ID and classified by what they govern. Do not replace an approved reference with an assistant-generated interpretation without the user's approval.

## Reference index

| ID | Uploaded source | Governs | Priority |
|---|---|---|---|
| REF-001 | `IMG_8150.png` | Main desktop application composition and dashboard density. The image is a HorizonX share capture containing a Nocturne-style dark dashboard. Use the dashboard shown inside the capture as the layout reference, not the mobile browser chrome. | Primary |
| REF-002 | `IMG_8151.jpeg` | FMRTE advanced-search dialog: category navigation, entity tabs, grouped filter panels, position pitch, numeric ranges and bottom actions. | Exact functional/layout reference |
| REF-003 | `IMG_8152.png` | Watched Earth visual language: black canvas, off-white typography, orange accent, editorial display type, monospaced metadata, thin framing lines and restrained controls. | Primary visual identity reference |

### External references

- [Watched Earth](https://www.watched.earth/) — visual language and typography reference.
- [HorizonX](https://horizonx.so/) — source page shown in REF-001. The embedded dashboard is the relevant design reference.
- [FMRTE](https://www.fmrte.com/) — product and feature reference; REF-002 is the user's chosen advanced-search layout reference.
- [FMRTE documentation](https://docs.fmrte.com/) — public feature documentation, not evidence of undocumented internals.

## Interpretation rules

1. REF-001 controls the overall desktop composition: compact persistent navigation, restrained dark panels, dense but legible working area and balanced dashboard cards.
2. REF-002 controls the advanced-search structure. Reproduce its information architecture and arrangement closely. Apply FM Atlas typography, colours and finish without redesigning the filter workflow.
3. REF-003 controls the brand language: near-black, warm off-white, orange accent, editorial headings and monospaced secondary metadata.
4. User's written instructions override incidental details in screenshots. In particular, do **not** add a Search item to the left navigation even if a reference screenshot contains one.
5. Screenshots are visual evidence, not proof of hidden behaviour. Every interaction must have a separate behaviour specification and test.
6. If a reference is ambiguous, record the ambiguity and ask before making a material design decision.
7. Do not use unrelated assistant-generated sample images as authoritative references when the user's uploaded images specify the same screen.

## Asset custody

The original image uploads are attached to the conversation as:
- `IMG_8150.png` — REF-001
- `IMG_8151.jpeg` — REF-002
- `IMG_8152.png` — REF-003

The screenshots have been inspected in the conversation. This GitHub document records their identities and interpretation; the binary image files have **not** been committed to the repository in this change. Keep the original uploads unchanged and add the actual image binaries to a repository asset folder when a binary-capable transfer is available.

## New reference intake

For each new reference:
- assign the next ID (`REF-004`, etc.);
- preserve original filename and upload;
- record screen/component, source URL if known, exact elements to reproduce, permitted adaptations and required states;
- link it from this index;
- update the relevant screen specification before implementation.
