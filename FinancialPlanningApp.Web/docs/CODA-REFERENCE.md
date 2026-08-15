# CODA Parsing Reference (Authoritative)

This project treats the official Febelfin CODA specification as the authoritative source for all CODA parsing and field-position logic.

## Canonical reference

- Febelfin CODA standard (English, 2025 publication):
  - https://febelfin.be/media/pages/publicaties/2023/febelfin-standaarden-voor-online-bankieren/d7168c5c37-1764229602/standard-coda-en_-2025.pdf

## Rule for contributors

- When implementing or modifying CODA parsing:
  1. First verify record layout and field positions against the Febelfin document above.
  2. Do not rely on assumptions from samples when they conflict with the standard.
  3. Document any bank-specific deviation explicitly in code comments and keep the standard behavior as default.

## Current parser assumptions aligned to the standard

- Record `12`:
  - Own account field is read from positions `6-39` (1-based).
  - If no valid account can be extracted from this field, CODA transactions are considered invalid for import enforcement.
- Record `8`:
  - Year component for unique source sequencing is read from balance date field `58-63` (`DDMMYY`, 1-based).
- Record `12` + Record `21`:
  - Paper number and transaction number are used to form source sequence and source reference keys.

## Change management

- If Febelfin publishes a newer standard version, update this file link and verify all fixed-position extraction in:
  - `Infrastructure/BankImport/CodaBankImportAdapter.cs`
  - related import validation and deduplication logic

