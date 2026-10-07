#ifndef SCENT_TRAIL_WAVE_INCLUDED
#define SCENT_TRAIL_WAVE_INCLUDED

// Places a scent-trail particle on the line from the acorn to the player, pushed sideways by a
// sine wave that travels toward the player. Used by TrailEffect.vfx as a Custom HLSL block at the
// top of Render Strip. Progress along the line is the particle's age over its lifetime.
//
// Kept to 4 inputs (the settings are packed into one Vector4) so it stays within what a VFX
// Custom HLSL node accepts.
//
// settings.x  amplitude:  how far the wave swings sideways, in world units.
// settings.y  wavelength: world units per wave, so the wave keeps its shape when the distance changes.
// settings.z  waveSpeed:  waves per second travelling toward the player.
// settings.w  taper:      world units over which the wave eases in at the acorn and out at the player.
void ScentTrailWave(inout VFXAttributes attributes, in float3 acorn, in float3 player, in float time,
                    in float4 settings)
{
    float amplitude = settings.x;
    float wavelength = settings.y;
    float waveSpeed = settings.z;
    float taper = settings.w;

    float progress = attributes.lifetime > 0.0 ? saturate(attributes.age / attributes.lifetime) : 0.0;

    float3 toPlayer = player - acorn;
    float dist = length(toPlayer);
    float3 onLine = acorn + toPlayer * progress;

    if (dist < 0.0001 || wavelength <= 0.0)
    {
        attributes.position = onLine;
        return;
    }

    float3 dir = toPlayer / dist;
    float3 side = float3(-dir.y, dir.x, 0.0);   // perpendicular in the 2D plane

    const float TAU = 6.2831853;

    // Different phases per acorn, so neighbouring trails don't wave in step.
    float seedA = frac(sin(dot(acorn.xy, float2(12.9898, 78.233))) * 43758.5453);
    float seedB = frac(sin(dot(acorn.xy, float2(39.3468, 11.135))) * 43758.5453);
    float seedC = frac(sin(dot(acorn.xy, float2(73.1561, 52.235))) * 43758.5453);

    float along = progress * dist;   // world units from the acorn
    float waves = along / wavelength;

    // Main sine plus two longer, slower cosines, so the shape drifts instead of repeating. Every
    // layer is at least the base wavelength long, so wavelength is the finest bend the trail ever
    // makes and it stays smooth. The 1.63 and 2.71 ratios don't divide evenly into each other, so
    // the layers never line up the same way twice. Weights sum to 1, keeping amplitude the max swing.
    float wave = 0.55 * sin((waves - time * waveSpeed + seedA) * TAU)
               + 0.30 * cos((waves / 1.63 - time * waveSpeed * 0.71 + seedB) * TAU)
               + 0.15 * cos((waves / 2.71 - time * waveSpeed * 0.37 + seedC) * TAU);

    // Pin both ends to the acorn and the player so the wave only swings in between.
    float ends = smoothstep(0.0, taper, along) * smoothstep(0.0, taper, dist - along);

    attributes.position = onLine + side * wave * amplitude * ends;
}

#endif
