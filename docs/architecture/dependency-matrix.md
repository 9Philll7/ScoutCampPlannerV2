# Module Dependency Matrix

## Principle

Dependencies are directional.

Data ownership:

Lager structure is the foundation.

Other modules may consume its information but must not directly modify it.

## Current dependencies

| Module | Can read | Can modify |
|---|---|---|
| Platform | - | Own data |
| Camp | Platform contracts | Own data |
| Catering | Camp contracts | Own data |
| Finance | Camp/Catering contracts | Own data |
| Program | Camp contracts | Own data |
| Material | Camp contracts | Own data |

## Future modules

The same principle applies.
