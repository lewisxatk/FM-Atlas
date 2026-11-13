# R4.2

- Added packed DOB read candidates and safe date validation.
- Added non-destructive LivePlayers migrations for DOB and generated-player classification/evidence.
- Added age filtering for live FM records with an explicit age-as-of date input.
- Added FMRTE-style Regen Yes / No / Either filter semantics.
- Kept unclassified regen status as Unknown; did not invent an automatic classifier from UID or facepack mapping.
- Preserved existing attribute import, confidence labels and shortlist database.
