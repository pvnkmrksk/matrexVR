# Closed Loop Conversion Changes - For Posterity

## Old Conversion (Before Changes)

**Input**: `currentFicTracData.z` = l-r in radians from ZMQ

**Processing**:
1. Converted to degrees: `absoluteYaw = currentFicTracData.z * Mathf.Rad2Deg`
2. DC offset was stored in **degrees** (default: 0.0f, step: 0.1 degrees)
3. Formula: `rotationDelta = yawGain * (absoluteYaw - yawDCOffset)`
   - Where both `absoluteYaw` and `yawDCOffset` were in **degrees**
   - Result: degrees per second
4. Applied: `transform.Rotate(0, rotationDelta * Time.deltaTime, 0, Space.Self)`

**Summary**: Everything was in degrees - input converted from radians to degrees, DC offset in degrees, calculation in degrees.

## New Conversion (After Changes)

**Input**: `currentFicTracData.z` = l-r in radians from ZMQ

**Processing**:
1. Keep in radians: `yawRadians = currentFicTracData.z` (no conversion)
2. DC offset is stored in **radians** (default: 0.0f, step: 0.01745 rad ≈ 0.1 degrees)
3. Formula: `rotationDeltaRadians = yawGain * (yawRadians - yawDCOffset)`
   - Where both `yawRadians` and `yawDCOffset` are in **radians**
   - Result: radians per second
4. Convert to degrees for Unity: `rotationDeltaDegrees = rotationDeltaRadians * Mathf.Rad2Deg`
5. Applied: `transform.Rotate(0, rotationDeltaDegrees * Time.deltaTime, 0, Space.Self)`

**Summary**: Calculation is done in radians (matching the input units), then converted to degrees only for Unity's Rotate() function.

## Key Differences

- **Old**: Input converted to degrees → calculation in degrees → applied in degrees
- **New**: Input stays in radians → calculation in radians → converted to degrees only for Unity

The mathematical result is the same, but the new approach is more consistent with the input data format (radians from ZMQ).

