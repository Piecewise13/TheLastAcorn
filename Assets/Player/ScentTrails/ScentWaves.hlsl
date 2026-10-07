#ifndef SCENT_WAVES_INCLUDED
#define SCENT_WAVES_INCLUDED

// Random 0..1 from a ray index.
float ScentHash(float n)
{
    return frac(sin(n * 127.1) * 43758.5453);
}

// Wiggling scent lines radiating out from Center. Used by SenseOverlay.shadergraph through a
// Custom Function node (Type: File, Name: ScentWaves). The node's inputs must match this order.
// Reveal is wired to SenseAmount. Pick is a random 0..1 per ray, for sampling a colour gradient.
void ScentWaves_float(float2 UV, float2 Center, float Aspect, float Time, float Rays,
                      float WiggleAmp, float WiggleFreq, float Speed, float Width,
                      float InnerRadius, float InnerFalloff, float Reveal, out float Out, out float Pick)
{
    const float TAU = 6.2831853;

    // Whole number of rays, so the last ray meets the first where atan2 wraps (left of the player).
    float rays = max(1.0, round(Rays));

    float2 d = (UV - Center) * float2(Aspect, 1.0);
    float r = length(d);
    float a = (atan2(d.y, d.x) / TAU + 0.5) * rays;   // 0..rays around the circle
    float cell = floor(a);

    // Check this cell's ray and both neighbours: a wiggling ray can lean into the next cell,
    // and testing only its own cell is what cuts it off.
    float ray = 0.0;
    float pick = 0.0;
    [unroll]
    for (int o = -1; o <= 1; o++)
    {
        float id = cell + o;
        float wrapped = id - rays * floor(id / rays);   // same seeds on both sides of the seam
        float seedA = ScentHash(wrapped);
        float seedB = ScentHash(wrapped + 17.31);
        float seedC = ScentHash(wrapped + 41.77);
        float seedD = ScentHash(wrapped + 73.19);
        float seedE = ScentHash(wrapped + 97.13);

        // Each ray gets its own speed, wavelength and strength so they stop moving in step.
        float speed = Speed * lerp(0.6, 1.4, seedB);
        float freq = WiggleFreq * lerp(0.7, 1.3, seedC);
        float amp = WiggleAmp * lerp(0.5, 1.0, seedD);

        // Main wiggle travelling outward, a cos at a different wavelength and speed layered on
        // top so the shape keeps changing, and a slow sway of the whole ray. Weights sum to 1.
        float wiggle = 0.6 * sin(r * freq - Time * speed + seedA * TAU)
                     + 0.25 * cos(r * freq * 1.7 - Time * speed * 0.6 + seedB * TAU)
                     + 0.15 * sin(Time * lerp(0.3, 0.8, seedC) + seedD * TAU);

        float centre = id + 0.5 + wiggle * amp;

        // distance from the ray in screen units, so rays don't fatten with distance
        float across = abs(a - centre) * (TAU * r / rays);
        float strength = 1.0 - smoothstep(0.0, Width, across);

        // Where rays overlap, the strongest one decides the colour, so each ray keeps its own.
        pick = strength > ray ? seedE : pick;
        ray = max(ray, strength);
    }

    // Reveal 0 -> 1 sweeps the inner edge in from the outer fade's edge (r = 1) to InnerRadius,
    // so the waves grow toward the player like the scent trails do. Going back to 0 sweeps them
    // out. Linear on purpose: SenseVisuals eases _SenseAmount once, so this stays in step with
    // the saturation fade that uses the same value.
    float inner = lerp(1.0, InnerRadius, saturate(Reveal));

    // fade in gradually away from the player, fade out toward the screen edge
    float fade = smoothstep(inner, inner + InnerFalloff, r)
               * (1.0 - smoothstep(0.6, 1.0, r));

    Out = ray * fade;
    Pick = pick;
}

#endif
