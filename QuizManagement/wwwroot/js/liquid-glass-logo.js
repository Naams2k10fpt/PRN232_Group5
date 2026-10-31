"use strict";
(function () {
    /*!
    ReactDrops Component License 1.0
    
    Copyright (c) 2026 ReactDrops
    
    1. Scope and definitions
    
    This license applies to ReactDrops software and associated documentation that
    expressly carry this license or are distributed under a LICENSE file containing
    it (the "Software"). "Licensor" means the copyright holder granting rights in
    the Software, and only to the extent that holder has authority to grant them.
    "You" means the individual or entity exercising these rights.
    
    An "End Product" is a website, application, service, or other finished project
    whose primary purpose is something other than providing reusable components,
    source code, templates, or development assets to others.
    
    2. Permitted uses
    
    Subject to this license, Licensor grants You a worldwide, non-exclusive,
    royalty-free license to use, copy, modify, and incorporate the Software into
    unlimited personal, internal, and commercial End Products. You may charge for
    End Products, subscriptions, development services, and work for clients.
    
    You may distribute the Software as an integrated part of an End Product,
    including in browser-delivered JavaScript. You may provide End Product source
    code to a client, and share the Software with employees and contractors who
    need it to develop or maintain End Products. Those recipients may use the
    Software for those purposes subject to this license. This permission does not
    authorize them to redistribute the Software as a reusable development asset.
    
    3. Restricted redistribution
    
    Unless You obtain separate written permission from Licensor, You may not sell,
    sublicense, publish, share, or otherwise redistribute the Software, or a
    substantial portion of it, whether unchanged or modified:
    
    (a) as a standalone component, component library, UI kit, source-code bundle,
        registry, or other collection of reusable development assets;
    (b) as part of a theme, template, starter project, or similar product offered
        to multiple recipients for reuse in their own projects; or
    (c) through a builder or generation service that offers the Software as
        reusable blocks or source-code exports for others' projects.
    
    These restrictions apply to paid and free redistribution. Renaming, restyling,
    repackaging, translating, or modifying the Software does not remove them.
    Building separate finished websites or applications for multiple clients is
    permitted; offering a reusable template or kit containing the Software is not.
    
    Public source-code hosting by Licensor is not permission to republish the
    Software under another license. This license does not override any separate
    rights granted directly by Licensor or applicable code-hosting terms.
    
    4. Notices and ownership
    
    Retain this license and applicable copyright and third-party notices in source
    copies of the Software and permitted source distributions. No visible credit
    or ReactDrops logo is required in an End Product's user interface.
    
    Licensor retains ownership of its Software. You retain rights in Your own
    contributions, subject to the license for any incorporated Software. No rights
    to trademarks, trade names, or logos are granted by this license.
    
    5. Third-party materials and separate grants
    
    Dependencies, fonts, images, videos, and other third-party materials remain
    subject to their respective licenses. Separately identified third-party code
    is licensed under its stated terms, and these restrictions do not limit rights
    granted by its copyright holders. Retain all required third-party notices.
    Demo media are not licensed by this document; obtain Your own permission or
    replace them with media You are entitled to use.
    
    This license does not revoke rights granted under other valid licenses. It does
    not assert ownership of third-party work or grant rights Licensor lacks.
    
    6. Termination
    
    The permissions granted here continue unless terminated for a material breach.
    If You fail to cure a material breach within 30 days after receiving written
    notice from Licensor, Your permissions terminate. On termination, cease uses
    and distributions requiring this license. Rights under independently granted
    licenses and rights that applicable law does not allow to be restricted remain
    unaffected. Sections 3 through 7 survive termination.
    
    7. Disclaimer
    
    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO WARRANTIES OF MERCHANTABILITY, FITNESS
    FOR A PARTICULAR PURPOSE, TITLE, AND NON-INFRINGEMENT. TO THE MAXIMUM EXTENT
    PERMITTED BY APPLICABLE LAW, LICENSOR AND CONTRIBUTORS SHALL NOT BE LIABLE FOR
    ANY CLAIM, DAMAGES, OR OTHER LIABILITY, WHETHER IN CONTRACT, TORT, OR OTHERWISE,
    ARISING FROM, OUT OF, OR IN CONNECTION WITH THE SOFTWARE OR ITS USE.
    
    This license does not limit rights or exceptions that applicable law makes
    non-waivable. No other permissions are granted by implication.
    */
    const liquidGlassLogoVertex = `#version 300 es
in vec2 aPosition;
void main() {
  gl_Position = vec4(aPosition, 0.0, 1.0);
}
`;
    // An optical surface, independent of the artwork beneath it. The first lens morphs
    // from a sphere to a superellipse; the second opens over the measured wordmark.
    // Refraction is integrated over the visible spectrum, rather than RGB offsets.
    // All distances are in CSS pixels, so the optics survive resizing and different DPRs.
    const liquidGlassLogoRender = `#version 300 es
precision highp float;
uniform sampler2D uLogo;
uniform sampler2D uText;
uniform vec2 uResolution;
uniform float uDpr;
uniform float uUnit;
uniform vec2 uLogoBox;
uniform vec2 uTextBox;
uniform float uTextWidth;
uniform float uTime;
uniform float uSettle;
uniform float uArtScale;
uniform float uWordmarkEnabled;
uniform vec3 uTile;
uniform float uTileEnabled;
// 0 on a dark page, 1 on a pale one.
uniform float uPageLight;
uniform float uDepth;
uniform float uDispersion;
uniform float uRelief;
uniform float uCaustics;
uniform float uHighlights;
// How much the faint line outside the rim takes the plate color.
uniform float uEchoTint;
// 0 draws the full inner wall; 1 keeps it to the upper-left and lower-right arcs.
uniform float uInnerWallFocus;
uniform float uDropSize;
uniform float uDropStrength;
uniform float uDecay;
// Second drop: radius, core radius, rim stretch, center factor (negative inverts).
uniform vec4 uDrop;
// Second drop: core edge magnification, profile exponent, ripple amplitude, wavelength.
uniform vec4 uDropLens;
// Second drop: ripple phase, share of ripples inside the core, dispersion, unused.
uniform vec4 uDropWave;
out vec4 outColor;
const float PI = 3.14159265359;
const int BANDS = 32;

float sq(float x) { return x * x; }
float bell(float x, float width) { return exp(-sq(x / max(width, 0.001))); }
float ease(float a, float b, float x) { return smoothstep(a, b, x); }

// Smooth overlapping spectral sensitivities, normalized in the integration below.
vec3 spectrum(float s) {
  float wavelength = 400.0 + 300.0 * s;
  vec3 rgb = vec3(0.0);
  if (wavelength < 440.0) rgb = vec3((440.0 - wavelength) / 120.0, 0.0, 1.0);
  else if (wavelength < 490.0) rgb = vec3(0.0, (wavelength - 440.0) / 50.0, 1.0);
  else if (wavelength < 510.0) rgb = vec3(0.0, 1.0, (510.0 - wavelength) / 20.0);
  else if (wavelength < 580.0) rgb = vec3((wavelength - 510.0) / 70.0, 1.0, 0.0);
  else if (wavelength < 645.0) rgb = vec3(1.0, (645.0 - wavelength) / 65.0, 0.0);
  else rgb = vec3(1.0, 0.0, 0.0);
  return rgb * mix(0.2, 1.0, ease(400.0, 430.0, wavelength)) * mix(1.0, 0.65, ease(650.0, 700.0, wavelength));
}
float glassIor(float s, float spread) {
  float wavelength = 0.4 + 0.3 * s;
  return 1.38 + (0.04 / (wavelength * wavelength) - 0.12) * spread;
}

vec4 imageAt(sampler2D image, vec2 p, vec2 box) {
  vec2 uv = 0.5 + p / box;
  // Transparent outside the image, including images that touch their texture edges.
  // Explicit LOD is stable inside the back-face branch; implicit derivatives are
  // undefined there and can make the same sought frame change after another draw.
  vec2 footprint = vec2(textureSize(image, 0)) / max(box * uDpr, vec2(1.0));
  float lod = max(0.0, log2(max(footprint.x, footprint.y)));
  // The first sphere comes into focus as it flattens into a plate.
  lod += (1.0 - ease(0.20, 0.36, uTime)) * 2.2;
  vec4 ink = textureLod(image, uv, lod);
  return ink * step(0.0, uv.x) * step(0.0, uv.y) * step(uv.x, 1.0) * step(uv.y, 1.0);
}

float superRadius(vec2 p, float n) {
  vec2 a = max(abs(p), vec2(0.00001));
  return pow(pow(a.x, n) + pow(a.y, n), 1.0 / n);
}
vec2 superNormal(vec2 p, float n) {
  return normalize(sign(p) * pow(max(abs(p), vec2(0.00001)), vec2(n - 1.0)) + vec2(0.000001));
}

float hash1(float n) { return fract(sin(n * 127.1 + 311.7) * 43758.5453); }

// Real glass is not a perfect stroke. Along an edge (u = arc length in pixels) its
// light varies slowly and breaks into short glints where the surface is uneven.
float contourNoise(float u) {
  float i = floor(u);
  float f = fract(u);
  f = f * f * (3.0 - 2.0 * f);
  return mix(hash1(i), hash1(i + 1.0), f);
}
float specks(float u, float d, float size, float seed) {
  float spacing = size * 22.0;
  float cell = floor(u / spacing);
  float on = step(0.7, hash1(cell + seed));
  float center = (cell + 0.2 + 0.6 * hash1(cell + seed + 17.0)) * spacing;
  float offset = (hash1(cell + seed + 31.0) - 0.5) * size * 0.6;
  // Glints are dashes along the edge, several times longer than they are wide.
  float dash = size * (2.5 + 4.0 * hash1(cell + seed + 53.0));
  return on * bell(u - center, dash) * bell(d - offset, size * 0.35);
}
// The same glints as particles held by the liquid: each one bobs across the edge
// on its own period and barely sways along it, so nothing slides sideways.
// Neighboring cells are included so a moving glint is not clipped at its cell.
float driftingSpecks(float u, float d, float size, float seed, float t) {
  float spacing = size * 22.0;
  float cell = floor(u / spacing);
  float glints = 0.0;
  for (int j = -1; j <= 1; j++) {
    float id = cell + float(j);
    float on = step(0.7, hash1(id + seed));
    float rate = 2.5 + 3.0 * hash1(id + seed + 71.0);
    float phase = 6.2832 * hash1(id + seed + 89.0);
    float center = (id + 0.2 + 0.6 * hash1(id + seed + 17.0)) * spacing + sin(t * rate * 0.5 + phase) * spacing * 0.05;
    float offset = (hash1(id + seed + 31.0) - 0.5) * size * 0.6 + sin(t * rate + phase) * size * 1.6;
    float dash = size * (2.5 + 4.0 * hash1(id + seed + 53.0));
    glints += on * bell(u - center, dash) * bell(d - offset, size * 0.35);
  }
  return min(glints, 1.0);
}

// Material-space relief: neighboring glass walls share the slow undulation but
// have different fine thickness variations. It stays attached as the lens grows.
float edgeWarp(float arc, float seed) {
  float broad = contourNoise(arc / (uUnit * 0.037) + seed) - 0.5;
  float fine = contourNoise(arc / (uUnit * 0.008) + seed + 29.0) - 0.5;
  return uUnit * (broad * 0.0017 + fine * 0.0008);
}

// Small flattened air pockets in the glass channel. x is the dark cavity;
// y is its light-catching lip. Three cells avoid popping at cell boundaries.
vec2 glassPores(float arc, float d, float seed) {
  if (abs(d) > uUnit * 0.011) return vec2(0.0);
  float pitch = uUnit * 0.022;
  float cell = floor(arc / pitch);
  vec2 pores = vec2(0.0);
  for (int j = -1; j <= 1; j++) {
    float id = cell + float(j) + seed;
    float visible = step(0.63, hash1(id));
    float center = (cell + float(j) + 0.15 + 0.7 * hash1(id + 17.0)) * pitch;
    float across = (hash1(id + 31.0) - 0.5) * uUnit * 0.009;
    float span = uUnit * (0.0022 + 0.0035 * hash1(id + 53.0));
    float height = max(0.35 / uDpr, uUnit * (0.0007 + 0.0012 * hash1(id + 71.0)));
    vec2 q = vec2((arc - center) / span, (d - across) / height);
    // A slight skew keeps the cavities from forming identical ellipses.
    q.x += q.y * (hash1(id + 89.0) - 0.5) * 0.55;
    float radius = length(q);
    float aa = max(0.18, 0.4 / (uDpr * height));
    float cavity = bell(radius, 0.65);
    float lip = bell(radius - 1.0, aa);
    float glint = 0.25 + 0.75 * max(dot(q / max(radius, 0.001), normalize(vec2(-0.4, 0.8))), 0.0);
    pores += vec2(cavity, lip * glint) * visible;
  }
  return min(pores, vec2(1.0));
}

// Light reflected off the ripples outside the glass is invisible on a pale page.
// There the same ripples bend the page away and read as cool shadows, as in the
// light reference. Coverage is unchanged, so a dark page renders as before.
vec3 exteriorColor(vec3 light, float coverage) {
  // The shadow leans toward the plate's own tone, a little deeper, so it belongs
  // to the glass instead of reading as a gray smudge on the page.
  vec3 shade = mix(vec3(0.40, 0.46, 0.56), uTile * 0.86, uEchoTint);
  return mix(light, shade * coverage, uPageLight);
}

// Per-channel coverage of the last refracted image. Each wavelength band lands
// on the artwork at its own offset, so a dark logo over a light plate blocks red,
// green and blue at slightly different places and its edges split into color.
vec3 refractedCoverage;

// A refracted image behind a glass lens, with a little light focused onto its ink.
vec4 refractedLogo(vec2 p, vec2 normalXY, float lensScale, float glass, float chromaticScale, float surfaceDistance) {
  vec3 normal = normalize(vec3(normalXY, 1.0));
  vec3 color = vec3(0.0);
  vec3 weights = vec3(0.0);
  float alpha = 0.0;
  float minCoverage = 1.0;
  float maxCoverage = 0.0;
  refractedCoverage = vec3(0.0);
  float spread = uDispersion * chromaticScale * mix(0.85, mix(2.2, 1.5, ease(0.28, 0.9, uTime)), ease(0.25, 0.33, uTime));
  vec3 neutralRay = refract(vec3(0.0, 0.0, -1.0), normal, 1.0 / glassIor(0.5, spread));
  vec2 neutralOffset = neutralRay.xy / max(-neutralRay.z, 0.25) * uUnit * uDepth * 0.80 * lensScale;
  for (int i = 0; i < BANDS; i++) {
    float s = float(i) / float(BANDS - 1);
    // Shorter wavelengths bend more. Dispersion is intentionally visible at the bevel.
    // The young sphere splits light only at its rim; the settled bevel splits it strongly.
    float ior = glassIor(s, spread);
    vec3 ray = refract(vec3(0.0, 0.0, -1.0), normal, 1.0 / ior);
    vec2 offset = ray.xy / max(-ray.z, 0.25) * uUnit * uDepth * 0.80 * lensScale;
    // Measured artwork scale over time; see artworkScale in the renderer.
    float artworkScale = uArtScale;
    float magnification = 1.0;
    // The opening focus folds the edge of the image into a partial crescent.
    // It opens into barrel distortion before the sphere flattens into the plate.
    float sphereRadius = uUnit * lensScale * 0.5;
    float sphereFocus = 1.0 - ease(0.225, 0.335, uTime);
    float bubbleQ = min(length(p) / max(sphereRadius, 1.0), 1.0);
    float openingFocus = 1.0 - ease(0.19, 0.265, uTime);
    float focusProfile = mix(0.7 + 0.6 * sq(bubbleQ), 1.0 - 0.9 * sq(bubbleQ), openingFocus);
    vec2 bubbleSample = p * focusProfile + offset * mix(0.35, 0.12, openingFocus);
    bubbleSample += vec2(0.025, 0.10) * uUnit * openingFocus;
    bubbleSample += (offset - neutralOffset) * openingFocus * 1.6;
    vec2 sourcePoint = mix(p + offset, bubbleSample, sphereFocus);
    vec4 ink = imageAt(uLogo, sourcePoint, uLogoBox * artworkScale * magnification);
    // Focus is inside the young dome, leaving clear glass between the enlarged
    // fragment and the rim. The aperture opens as the rest of the logo appears.
    ink *= mix(1.0, 1.0 - ease(0.72, 0.86, bubbleQ), openingFocus);
    // The rim is an optical aperture too. Giving each wavelength its own boundary
    // creates a red/yellow outer edge and cyan/blue inner edge without tinting the ink.
    float edgeShift = (s - 0.5) * uUnit * lensScale * 0.018 * uDispersion * chromaticScale;
    ink *= 1.0 - smoothstep(-0.8 / uDpr, 0.8 / uDpr, surfaceDistance - edgeShift);
    minCoverage = min(minCoverage, ink.a);
    maxCoverage = max(maxCoverage, ink.a);
    vec3 w = spectrum(s);
    color += ink.rgb * w;
    alpha += ink.a * (w.r + w.g + w.b);
    refractedCoverage += ink.a * w;
    weights += w;
  }
  color /= weights;
  refractedCoverage /= weights;
  // A coated glass shoulder has a cooler spectral response at a split image edge.
  // Coverage variance keeps solid areas and original-color artwork free of a tint.
  float coating = ease(-uUnit * lensScale * 0.022, 0.0, surfaceDistance);
  color.g *= 1.0 - 0.65 * coating * (maxCoverage - minCoverage);
  // The young curved face catches a violet interference fringe at internal ink
  // edges. Its outer lip stays cyan; opaque artwork keeps its original color.
  float youngFilm = (maxCoverage - minCoverage) * (1.0 - ease(0.19, 0.30, uTime));
  youngFilm *= 1.0 - ease(-uUnit * lensScale * 0.08, -uUnit * lensScale * 0.025, surfaceDistance);
  color.r += color.b * youngFilm * 0.80;
  color.b += color.r * youngFilm * 0.60;
  color.g *= 1.0 - youngFilm * 0.55;
  alpha /= weights.r + weights.g + weights.b;
  // This lift belongs to the image's edges, not a painted rainbow on the lens.
  float diffuse = max(dot(normal, normalize(vec3(-0.4, 0.6, 1.0))), 0.0);
  vec3 earlyWhite = mix(vec3(0.90, 0.93, 0.95), vec3(0.84, 0.86, 0.89), ease(0.21, 0.40, uTime));
  vec3 whiteBalance = mix(earlyWhite, vec3(0.87, 0.88, 0.885), ease(0.75, 1.22, uTime));
  color *= whiteBalance * ((0.90 + 0.10 * diffuse + glass * uCaustics * 0.04) * ((0.975 + 0.025 * sin(p.y / uUnit * PI)) * (1.0 + 0.09 * ease(0.75, 1.22, uTime) + 0.03 * (1.0 - ease(0.2, 0.5, uTime)))));
  return vec4(color, alpha);
}

vec4 iconLens(vec2 p) {
  float t = uTime;
  float settle = ease(0.0, 1.0, uSettle);
  float enter = ease(0.025, 0.16, t);
  vec4 source = imageAt(uLogo, p, uLogoBox * uArtScale);
  if (t > 1.58) return source * settle;
  if (enter < 0.001) return vec4(0.0);
  float shape = 2.0 + 3.0 * ease(0.29, 0.36, t);
  // The sphere appears quickly, then the glass continues expanding after the
  // corners have squared off. Completing both motions at once feels abrupt.
  float expand = enter * (0.64 + 0.41 * (1.0 - exp(-max(t - 0.16, 0.0) * 5.8)));
  float collapse = 1.0 - ease(1.33, 1.52, t);
  float lensScale = max(0.001, mix(expand * mix(0.48, 1.0, collapse), 1.0, settle));
  float halfSize = uUnit * 0.5 * lensScale;
  float radius = superRadius(p / halfSize, shape);
  float sd = (radius - 1.0) * halfSize;
  float aa = max(fwidth(sd), 0.65);
  float mask = (1.0 - smoothstep(-aa, aa, sd));
  float glass = (1.0 - ease(1.05, 1.4, t)) * collapse * (1.0 - settle);
  float shown = ease(0.12, 0.165, t) * (1.0 - ease(1.39, 1.53, t));
  // Nothing is drawn beyond the thin echo just outside the rim.
  if (sd > uUnit * 0.10) return vec4(0.0);
  vec2 outward = superNormal(p / halfSize, shape);
  // A flat center rolls into a thick, curved shoulder. The normal continuously bends
  // source rays into the plate and folds them at its rim, whatever the source image.
  vec2 radial = normalize(p + vec2(0.001));
  float diagonal = max(0.0, 2.0 * radial.x * radial.y);
  float shoulderStart = 0.6 + 0.18 * ease(0.66, 1.15, t) * (1.0 - diagonal) + 0.16 * ease(1.0, 1.22, t) * diagonal;
  float shoulder = ease(shoulderStart, 1.0, radius);
  float slope = (0.04 * radius + (0.85 + 0.2 * (1.0 - ease(0.28, 0.75, t))) * shoulder * shoulder) * glass * uDropStrength;
  // The early sphere is rounder and more strongly magnifying than the settled tile.
  slope += (1.0 - ease(0.18, 0.42, t)) * radius * 0.32 * uDropStrength;
  vec2 opticalDirection = normalize(mix(outward, normalize(p + vec2(0.001)), 0.95));
  // A mild astigmatic shoulder carries the image into opposite corners.
  float astigmatism = 0.8 + 0.4 * radial.x * radial.y;
  vec2 n = opticalDirection * slope * astigmatism * (1.0 - settle);
  vec4 ink = refractedLogo(p, n, lensScale, glass, 1.0, sd);
  float illumination = 0.5 + 0.5 * dot(outward, normalize(vec2(-0.5, 0.9)));
  vec3 inkCoverage = refractedCoverage;
  // A dark plate deepens toward its rim; a pale frosted plate stays nearly even.
  float plateLight = ease(0.3, 0.8, dot(uTile, vec3(0.299, 0.587, 0.114)));
  vec3 body = uTile * (mix(0.55, 0.96, plateLight) + mix(0.45, 0.04, plateLight) * (1.0 - min(radius, 1.0)));
  body += vec3(0.004, 0.007, 0.01) * bell(radius - 0.75, 0.5);
  vec3 color = ink.rgb + body * (1.0 - inkCoverage) * glass * uTileEnabled;
  // On a pale page the plate's coverage follows the glass exactly, so its color
  // keeps the same hue while it melts into the page instead of dipping and
  // flaring. A dark page keeps its original fade.
  float plateFade = mix((1.0 - ease(1.08, 1.36, t)) * (1.0 - settle), glass, uPageLight);
  float alpha = max(ink.a, plateFade * uTileEnabled);
  // A broad studio light is reflected by the curved front face of the sphere.
  // It fades as the face flattens, revealing the dark glass beneath the artwork.
  vec2 spherePoint = p / halfSize;
  vec3 sphereNormal = normalize(vec3(spherePoint, sqrt(max(0.001, 1.0 - dot(spherePoint, spherePoint)))));
  vec3 sphereRay = reflect(vec3(0.0, 0.0, -1.0), sphereNormal);
  float lightTravel = mix(-0.12, 0.20, ease(0.13, 0.30, t));
  float softbox = bell(sphereRay.x - lightTravel, 1.05) * bell(sphereRay.y - 0.56, 0.52);
  float sphericalLight = softbox * (1.0 - ease(0.245, 0.36, t)) * glass * uHighlights * 0.70;
  color += vec3(0.50, 0.61, 0.72) * sphericalLight * (1.0 - ink.a * 0.85);
  alpha = max(alpha, sphericalLight);

  // Thin nested surface reflections, with asymmetric key light and a dim cool fill.
  // Material coordinates stay fixed during expansion and deterministic seeking.
  float arc = atan(p.y, p.x) * uUnit * 0.5;
  float grainScale = uUnit * 0.03;
  float developed = ease(0.23, 0.48, t);
  float cornerExposure = ease(0.12, 0.8, -2.0 * radial.x * radial.y);
  float reliefScale = lensScale * developed * (0.2 + 0.8 * cornerExposure);
  float sharedWarp = edgeWarp(arc, 0.0) * reliefScale;
  float rimSd = sd + sharedWarp;
  float innerSd = sd + sharedWarp * 0.7 + edgeWarp(arc, 41.0) * reliefScale * 0.4;
  float inner2Sd = sd + sharedWarp * 0.45 + edgeWarp(arc, 83.0) * reliefScale * 0.6;
  float thickness = 0.75 + 0.5 * contourNoise(arc / (uUnit * 0.013) + 13.0);
  // The bright rim line is a liquid edge, not an etched one. Its segments shimmer
  // in place and it bobs slightly across the edge as standing waves, so nothing
  // slides along it; its glints move as separate particles.
  float around = arc / (uUnit * 0.5);
  float rimBob = uUnit * 0.0025 * developed * (0.6 * sin(7.0 * around + 0.4) * sin(t * 6.3 + 1.0) + 0.4 * sin(3.0 * around + 2.0) * sin(t * 4.1));
  float rimLineSd = rimSd + rimBob;
  float shimmer = mix(contourNoise(arc / grainScale), contourNoise(arc / grainScale + 211.0), 0.5 + 0.5 * sin(t * 5.0 + 6.2832 * contourNoise(arc / (grainScale * 3.0) + 7.0)));
  float rim = bell(rimLineSd + uUnit * 0.002, uUnit * 0.0018 * thickness + aa * 0.3) * (0.6 + 0.8 * shimmer);
  // The nested lines nearer the center clear first; the outer rim lingers.
  float centerFade = 1.0 - ease(0.62, 0.92, t);
  float inner = centerFade * bell(innerSd + uUnit * 0.012, uUnit * 0.0025 / thickness + aa * 0.2) * (0.55 + 0.9 * contourNoise(arc / grainScale + 41.0));
  float inner2 = centerFade * bell(inner2Sd + uUnit * 0.022, uUnit * 0.0018 * thickness + aa * 0.1) * (0.5 + contourNoise(arc / grainScale + 83.0));
  float key = pow(illumination, 5.0);
  float rimVisibility = 1.0 - 0.55 * ease(0.75, 1.22, t);
  float lowerRight = pow(0.5 + 0.5 * dot(outward, normalize(vec2(0.8, -0.6))), 5.0);
  lowerRight *= ease(-0.25, -0.05, p.x / uUnit) * (1.0 - ease(-0.22, -0.02, p.y / uUnit));
  // Two separated walls, not two strokes at the same edge. The back wall is
  // inset by 7% of a tile edge, leaving a curved transmission channel between them.
  vec2 insetPoint = p - vec2(0.002, -0.002) * uUnit * lensScale;
  float wallGap = (0.055 - 0.023 * ease(0.18, 0.34, t)) + 0.050 * ease(0.14, 0.50, t) * (1.0 - 0.5 * ease(0.80, 1.22, t));
  float insetHalf = halfSize - wallGap * uUnit * lensScale;
  // The back face rounds off slightly later, giving the sphere and tile two visible
  // moving boundaries rather than scaling a second outline with the front face.
  float insetShape = max(2.0, 2.0 + (shape - 2.0) * ease(0.20, 0.55, t) - 0.45);
  float insetRadius = superRadius(insetPoint / insetHalf, insetShape);
  float insetSd = (insetRadius - 1.0) * insetHalf + sharedWarp * 0.4 + edgeWarp(arc, 151.0) * reliefScale * 0.7;
  float channel = ease(-uUnit * 0.008, uUnit * 0.025, insetSd) * mask;
  float echoMask = channel * lowerRight * glass * (1.0 - settle);
  vec3 backFace = vec3(0.0);
  float logoLight = 0.0;
  if (echoMask * uRelief > 0.002) {
    vec2 backNormal = normalize(mix(superNormal(insetPoint / insetHalf, insetShape), radial, 0.6));
    float backSlope = ease(0.90, 1.10, insetRadius) * 1.55 * glass * uDropStrength;
    // Sample a separate transmitted image across the whole bevel channel. Its
    // changing normal pulls a second, chromatically split copy of the logo into it.
    // A thick rim magnifies whatever lies closest behind it. Walk inward from the
    // wall until the ray meets ink and refract from there, so every logo lights
    // this corner from its own nearest shape; a logo with ink at the corner (like
    // X) is found on the first step and keeps its direct image.
    // The search starts where the wall already sends the ray, not at the wall.
    vec2 inward = -normalize(insetPoint + vec2(0.001));
    vec3 backRay = refract(vec3(0.0, 0.0, -1.0), normalize(vec3(backNormal * backSlope, 1.0)), 1.0 / 1.38);
    vec2 landing = insetPoint + backRay.xy / max(-backRay.z, 0.25) * uUnit * uDepth * 0.80 * lensScale;
    float reach = 0.0;
    for (int i = 0; i < 20; i++) {
      reach = float(i) * uUnit * lensScale * 0.03;
      if (imageAt(uLogo, landing + inward * reach, uLogoBox * uArtScale).a > 0.2) break;
    }
    vec4 backImage = refractedLogo(insetPoint + inward * reach, backNormal * backSlope, lensScale, glass, 1.6, sd);
    backFace = backImage.rgb * vec3(0.03, 0.65, 1.0) * echoMask * 0.48 * uRelief * (1.0 - 0.70 * ease(0.75, 1.12, t));
    // The lower-right light is this same refracted image, not a fixed lamp: it
    // only lights where the back wall bends logo ink into the bevel, and takes
    // that ink's shape as the logo or the refraction changes.
    logoLight = ease(0.05, 0.6, backImage.a);
  }
  lowerRight *= logoLight;
  float wallRim = centerFade * bell(insetSd, uUnit * 0.0028 + aa * 0.25);
  float wallShadow = centerFade * bell(insetSd + uUnit * 0.008, uUnit * 0.005);
  vec2 pores = vec2(0.0);
  if (developed * cornerExposure > 0.001) {
    pores = glassPores(arc, (rimSd + uUnit * 0.003) / lensScale, 313.0);
    pores += glassPores(arc, insetSd / lensScale, 617.0) * 0.6 * centerFade;
    pores += glassPores(arc, (inner2Sd + uUnit * 0.027) / lensScale, 919.0) * 0.8 * centerFade;
    pores *= developed * cornerExposure;
  }
  float angle = atan(p.y, p.x);
  float upperSliver = 0.12 + 1.15 * bell(angle - 2.18, 0.32);
  float lowerSliver = 0.12 + 1.10 * bell(angle + 0.85, 0.30);
  // The inner wall holds a brighter line around the upper-left and lower-right
  // corners, whatever the key light does. Each arc runs farther along the top or
  // bottom edge than down the side: its window is wider toward the horizontal.
  float tlOffset = angle - 0.75 * PI;
  float brOffset = angle + 0.25 * PI;
  float tlReach = tlOffset < 0.0 ? 0.65 : 0.42;
  float brReach = brOffset < 0.0 ? 0.65 : 0.42;
  float wallCorner = max(
    1.0 - ease(0.6 * tlReach, tlReach, abs(tlOffset)),
    1.0 - ease(0.6 * brReach, brReach, abs(brOffset))
  );
  vec3 insetLight = vec3(0.60, 0.78, 0.91) * wallRim * (key * key * 0.8 * upperSliver + lowerRight * lowerRight * 0.75 * lowerSliver + 0.005 + 0.35 * wallCorner * uInnerWallFocus);
  // The caustic follows the curved channel: a white-cyan core with blue lips, streaked
  // by the glass it passes through.
  // Filaments run parallel to the edge; their brightness drifts slowly along it.
  float drift = 0.7 + 0.6 * contourNoise(arc / (uUnit * 0.09) + 7.0);
  float filaments = (bell(innerSd + uUnit * 0.021, uUnit * 0.0035 * thickness) * 0.9 + bell(inner2Sd + uUnit * 0.028, uUnit * 0.004 / thickness) * 0.7) * centerFade + bell(rimSd + uUnit * 0.035, uUnit * 0.003 * thickness) * 0.5;
  vec3 lowerRelief = (
    vec3(0.03, 0.32, 1.0) * bell(sd + uUnit * 0.038, uUnit * 0.022 + aa * 0.2) * 0.55 +
    vec3(0.22, 0.62, 1.0) * bell(sd + uUnit * 0.027, uUnit * 0.012 + aa * 0.1) * 0.7 +
    vec3(0.75, 0.97, 1.0) * filaments * 1.35
  ) * drift * lowerRight * glass * uHighlights * uRelief * (1.0 - 0.85 * ease(0.6, 1.0, t)) * (1.0 - settle);
  lowerRelief *= 1.0 - pores.x * 0.22;
  lowerRelief += vec3(0.45, 0.85, 1.0) * pores.y * 0.10 * lowerRight * glass * uHighlights * uRelief * (1.0 - settle);
  // With focus at 0 the inner wall is drawn whole. Raising it keeps the wall on a
  // pale plate to its strongly lit segments and its upper-left and lower-right
  // arcs, since there its weak light clips to white and its shadow would
  // otherwise outline the full contour.
  float wallLit = clamp(key * key * 0.8 * upperSliver + lowerRight * lowerRight * 0.75 * lowerSliver, 0.0, 1.0);
  float wallGate = mix(1.0, max(ease(0.2, 0.7, wallLit), wallCorner), uPageLight * uInnerWallFocus);
  color *= 1.0 - wallShadow * wallGate * uRelief * 0.12 * (1.0 - ink.a);
  color += backFace + lowerRelief + insetLight * wallGate * glass * uHighlights * uRelief * (1.0 - ink.a) * mix(rimVisibility, 0.9, key);
  alpha = max(alpha, max(lowerRelief.r, max(lowerRelief.g, lowerRelief.b)));
  float rimLight = rim * (0.07 + key * 0.55) + inner * (0.025 + key * 0.16) + inner2 * 0.025;
  // Micro-bubbles along the lit edges, strongest where the key and the lower fill land.
  float speckSize = max(uUnit * 0.0042, 0.8 / uDpr);
  float bubbles = driftingSpecks(arc, rimLineSd + uUnit * 0.002, speckSize, 0.0, t) * 0.9;
  bubbles += specks(arc, innerSd + uUnit * 0.012, speckSize * 0.8, 97.0) * 0.6 * centerFade;
  bubbles += specks(arc, insetSd, speckSize * 0.9, 191.0) * 0.5 * centerFade;
  rimLight += bubbles * (0.04 + key * 0.5 + lowerRight * 0.45);
  rimLight = rimLight * (1.0 - pores.x * 0.35) + pores.y * (key * 0.16 + lowerRight * 0.12);
  vec3 spectralRim = vec3(0.0);
  vec3 spectralWeights = vec3(0.0);
  for (int i = 0; i < BANDS; i++) {
    float s = float(i) / float(BANDS - 1);
    vec3 w = spectrum(s);
    spectralRim += w * bell(rimSd + (s - 0.5) * uUnit * 0.011 * uDispersion + uUnit * 0.003, uUnit * 0.002 * thickness);
    spectralWeights += w;
  }
  spectralRim /= spectralWeights;
  vec3 reflection = (vec3(0.6, 0.76, 0.9) * rimLight * 1.15 + spectralRim * (0.02 + ink.a * 0.32)) * uHighlights * glass * mix(rimVisibility, 1.2, key);
  float sphericalRim = (
    bell(sd + uUnit * lensScale * 0.008, uUnit * lensScale * 0.009) * 0.60 +
    bell(insetSd, uUnit * lensScale * 0.008) * 0.40
  ) * (0.25 + 0.75 * abs(outward.y)) * (1.0 - ease(0.235, 0.34, t));
  reflection += vec3(0.65, 0.80, 0.90) * sphericalRim * 1.4 * uHighlights * glass;
  float cyanKey = pow(max(0.0, -outward.x), 8.0) * (1.0 - ease(0.235, 0.34, t));
  float cyanLip = bell(sd + uUnit * lensScale * 0.020, uUnit * lensScale * 0.009);
  cyanLip += bell(insetSd, uUnit * lensScale * 0.005) * 0.35;
  reflection += vec3(0.015, 0.68, 1.0) * cyanLip * cyanKey * glass * uHighlights * 0.85;
  // The young drop is full of scattered light: a milky glow behind the artwork.
  float youth = 1.0 - ease(0.25, 0.37, t);
  reflection += vec3(0.78, 0.84, 0.9) * youth * 0.07 * (0.5 + 0.5 * radius) * (1.0 - ink.a) * glass;
  vec3 film = mix(vec3(0.32, 0.0, 0.20), vec3(0.32, 0.08, 0.02), ease(0.75, 1.12, t)) * bell(sd + uUnit * 0.001, uUnit * 0.002) * ink.a * glass * uHighlights;
  color += reflection + film;
  alpha = max(alpha, max(reflection.r, max(reflection.g, reflection.b)));
  vec4 result = vec4(color * mask, alpha * mask);

  // Reflections outside the body are faint glass echoes, not solid colored outlines.
  float outer = bell(rimSd - uUnit * 0.012, uUnit * 0.0028 * thickness) * (0.04 + key * 0.2) * (0.5 + contourNoise(arc / grainScale + 131.0));
  outer += specks(arc, rimSd - uUnit * 0.012, speckSize, 277.0) * (0.03 + key * 0.3 + lowerRight * 0.25);
  // A single wave leaves the rim as the plate forms, stays close to it and fades.
  float waveAge = ease(0.36, 1.1, t);
  float wave = bell(sd - uUnit * (0.014 + 0.03 * waveAge), uUnit * 0.004 + aa * 0.3) * 0.05 * ease(0.34, 0.42, t) * (1.0 - waveAge);
  // The echo is lit through the plate, so it can carry the plate's hue and, on a
  // dark page, stay quieter than the rim it echoes.
  vec3 echoTint = mix(vec3(0.43, 0.62, 0.79), uTile + (1.0 - uTile) * 0.4, 0.5 * uEchoTint);
  float echoLevel = mix(1.0, mix(0.6, 1.0, uPageLight), uEchoTint);
  // The echo line runs around all four sides. The wave stays faint on the left
  // and right, where a full ring would read as streaks beside the glass.
  float waveSides = mix(1.0, 0.10, ease(0.25, 0.80, abs(outward.x)));
  vec3 ghost = (echoTint + ink.rgb * 0.6) * (outer + wave * waveSides) * glass * uHighlights * rimVisibility * echoLevel;
  float exterior = max(ghost.r, max(ghost.g, ghost.b)) * 1.8;
  result.rgb += exteriorColor(ghost * 1.8, exterior);
  result.a = max(result.a, exterior);
  // Resolve directly to the source, retaining its full size after the glass fades.
  return mix(result * shown, source, settle);
}

// The second drop is a radial lens with spherical aberration. Its core magnifies the
// wordmark (inverting it while the drop is tall), so between center and edge the image
// runs through focus and floods the drop; across its rim a thin ring of the image is
// stretched outward, and ripples crossing the rim repeat that ring in bands.
vec4 wordmarkLens(vec2 p) {
  float t = uTime;
  if (uWordmarkEnabled < 0.5) return vec4(0.0);
  if (t < 1.46) return vec4(0.0);
  if (t >= 3.3) return imageAt(uText, p, uTextBox);
  float radius = max(uDrop.x, 1.0);
  float r = length(p);
  if (r > radius + uUnit * 0.6) return vec4(0.0);
  vec2 outward = p / max(r, 0.001);
  float q = r / radius;
  float aa = 1.0 / uDpr;
  float edge = radius - r;
  float mask = ease(-aa, aa, edge);
  float young = 1.0 - ease(1.72, 2.02, t);

  float core = min(uDrop.y, radius);
  float coreQ = r / max(core, 1.0);
  float edgeLanding = core / max(uDropLens.x, 0.01);
  float center = uDrop.w;
  float landing = r < core
    ? edgeLanding * coreQ * (center + (1.0 - center) * pow(coreQ, uDropLens.y))
    : edgeLanding + (r - core) * uDrop.z;
  float rimZone = ease(core * 0.9, core, r);
  float k = 2.0 * PI / max(uDropLens.w, 1.0);
  landing += uDropLens.z * sin(k * r - uDropWave.x) * ease(0.0, uDropLens.w, r) * mix(uDropWave.y, 1.0, rimZone);
  vec2 bend = outward * (landing - r);
  float spread = uDropWave.z * uDispersion * (0.15 + 0.85 * rimZone);

  vec3 color = vec3(0.0);
  vec3 weights = vec3(0.0);
  float alpha = 0.0;
  float opacity = ease(1.46, 1.49, t);
  for (int i = 0; i < BANDS; i++) {
    float s = float(i) / float(BANDS - 1);
    vec2 source = p + bend * (1.0 + spread * (s - 0.5));
    vec4 ink = imageAt(uText, source, uTextBox);
    vec3 w = spectrum(s);
    color += ink.rgb * w;
    alpha += ink.a * (w.r + w.g + w.b);
    weights += w;
  }
  color /= weights;
  alpha /= weights.r + weights.g + weights.b;
  color *= 1.0 + uCaustics * bell(edge, uUnit * 0.06) * 0.15;
  color *= mask;
  alpha *= mask;
  // A fresh drop is full of scattered light: overexposed ink, a milky body and a cool
  // spill around it, all fading as it flattens.
  float fresh = 1.0 - ease(1.62, 1.95, t);
  color = min(color * (1.0 + 0.6 * fresh), vec3(1.0));
  float milk = mask * fresh * 0.30 * (0.6 + 0.4 * q) * uCaustics;
  color += vec3(0.75, 0.82, 0.9) * milk * (1.0 - alpha);
  float spill = exp(-max(r - radius, 0.0) / (uUnit * 0.17)) * (1.0 - mask) * fresh * 0.12;
  // As it spreads, the drop keeps a faint blue haze that dies with the waves.
  spill += exp(-max(r - radius, 0.0) / (uUnit * 0.4)) * (1.0 - fresh) * 0.035 * exp(-max(t - 1.95, 0.0) / 0.45) * (1.0 - ease(2.8, 3.1, t));
  color += vec3(0.32, 0.6, 1.0) * spill;
  alpha = max(alpha, max(milk, spill));
  vec3 sphereNormal = normalize(vec3(p / radius, sqrt(max(0.005, 1.0 - q * q))));
  vec3 reflected = reflect(vec3(0.0, 0.0, -1.0), sphereNormal);
  float softbox = bell(reflected.x + 0.3, 0.2) * bell(reflected.y - 0.55, 0.15);
  float smallLight = bell(reflected.x - 0.25, 0.14) * bell(reflected.y - 0.65, 0.08);
  float broadLight = bell(reflected.x + 0.35, 0.65) * bell(reflected.y - 0.6, 0.45);
  float surface = (softbox * 0.8 + smallLight * 0.6 + broadLight * 0.22) * young * uHighlights * mask;
  color += vec3(0.85, 0.95, 1.0) * surface;
  alpha = max(alpha, surface);

  float key = pow(0.5 + 0.5 * dot(outward, normalize(vec2(-0.6, 0.8))), 7.0);
  float rings = 0.0;
  for (int i = 0; i < 4; i++) {
    float j = float(i);
    float ringRadius = radius + (j - 1.0) * uUnit * 0.022 * (1.0 + 0.55 * ease(1.8, 3.1, t));
    rings += bell(r - ringRadius, uUnit * (0.003 + j * 0.001)) * (0.11 / (1.0 + j));
  }
  float age = max(0.0, t - 1.46);
  float fade = exp(-age / max(uDecay, 0.1)) * (1.0 - ease(2.65, 3.3, t));
  // Only the drop's own nested rims are lit; no broad glow or crest travels out
  // beyond it.
  float sideLight = pow(abs(outward.x), 10.0) * mix(1.0, 0.72, step(0.0, p.x));
  vec3 reflection = vec3(0.4, 0.62, 0.79) * rings * (0.3 + key * 2.0 + sideLight * 1.8) * fade * uHighlights;
  color += reflection;
  alpha = max(alpha, max(reflection.r, max(reflection.g, reflection.b)));
  return vec4(color, alpha) * opacity;
}

void main() {
  vec2 p = (gl_FragCoord.xy - uResolution * 0.5) / uDpr;
  vec4 icon = iconLens(p);
  vec4 wordmark = wordmarkLens(p);
  vec4 result = wordmark + icon * (1.0 - wordmark.a);
  // RGB and alpha use the same coverage; preserve saturated spectral fringes when
  // compositing onto transparent or colored backgrounds.
  result.a = clamp(max(result.a, max(result.r, max(result.g, result.b))), 0.0, 1.0);
  float dither = (fract(sin(dot(gl_FragCoord.xy, vec2(12.9898, 78.233))) * 43758.5453) - 0.5) / 255.0;
  outColor = vec4(clamp(result.rgb + dither * result.a, vec3(0.0), vec3(result.a)), result.a);
}
`;
    // The photographic finish is independent of the logo and uses premultiplied coverage.
    const liquidGlassLogoComposite = `#version 300 es
precision highp float;
uniform sampler2D uScene;
uniform vec2 uResolution;
uniform float uDpr;
uniform float uUnit;
uniform float uSettle;
uniform float uBloom;
uniform float uTime;
uniform float uFinish;
uniform float uPageLight;
out vec4 outColor;

// Filter premultiplied color and coverage together. The footprint is in CSS
// pixels, so the glass has the same optical softness on normal and retina screens.
vec4 softenedWaves(vec2 uv, float lod) {
  vec2 d = vec2(uDpr * 0.90) / uResolution;
  vec4 blurred = textureLod(uScene, uv, lod) * 0.25;
  blurred += textureLod(uScene, uv + vec2(d.x, 0.0), lod) * 0.125;
  blurred += textureLod(uScene, uv - vec2(d.x, 0.0), lod) * 0.125;
  blurred += textureLod(uScene, uv + vec2(0.0, d.y), lod) * 0.125;
  blurred += textureLod(uScene, uv - vec2(0.0, d.y), lod) * 0.125;
  blurred += textureLod(uScene, uv + d, lod) * 0.0625;
  blurred += textureLod(uScene, uv - d, lod) * 0.0625;
  blurred += textureLod(uScene, uv + vec2(d.x, -d.y), lod) * 0.0625;
  blurred += textureLod(uScene, uv + vec2(-d.x, d.y), lod) * 0.0625;
  return blurred;
}

void main() {
  vec2 uv = gl_FragCoord.xy / uResolution;
  vec4 base = textureLod(uScene, uv, 0.0);
  float offset = log2(max(uDpr, 0.5));
  // Only the exterior liquid is softened. The artwork and its final resting
  // silhouette retain their native sharpness and original coverage.
  float wavePhase = smoothstep(0.29, 0.41, uTime) * (1.0 - smoothstep(1.25, 1.50, uTime));
  float waveMask = 0.0;
  if (wavePhase > 0.001) {
    float enter = smoothstep(0.025, 0.16, uTime);
    float expand = enter * (0.64 + 0.41 * (1.0 - exp(-max(uTime - 0.16, 0.0) * 5.8)));
    float collapse = 1.0 - smoothstep(1.33, 1.52, uTime);
    float lensScale = mix(expand * mix(0.48, 1.0, collapse), 1.0, smoothstep(0.0, 1.0, uSettle));
    float halfSize = max(0.001, uUnit * lensScale * 0.5);
    float shape = 2.0 + 3.0 * smoothstep(0.29, 0.36, uTime);
    vec2 p = abs((gl_FragCoord.xy - uResolution * 0.5) / uDpr) / halfSize;
    float radius = pow(pow(p.x, shape) + pow(p.y, shape), 1.0 / shape);
    float distance = (radius - 1.0) * halfSize;
    waveMask = smoothstep(0.0, max(0.75, uUnit * 0.004), distance) * wavePhase;
    if (waveMask > 0.001) {
      base = mix(base, softenedWaves(uv, max(0.0, offset + 0.15)), waveMask * 0.94);
    }
  }
  vec3 diffused = textureLod(uScene, uv, offset + 2.5).rgb * 0.50;
  diffused += textureLod(uScene, uv, offset + 4.1).rgb * 0.32;
  diffused += textureLod(uScene, uv, offset + 5.4).rgb * 0.18;
  float light = max(base.r, max(base.g, base.b));
  // Bright ink retains its calibrated color; light diffuses into its surroundings.
  float effect = 1.0 - smoothstep(0.0, 1.0, uFinish);
  // Bloom is light spilling onto a dark page. A pale page already holds that
  // light, and adding it where the plate thins out reads as a flash.
  vec3 glow = diffused * uBloom * 0.20 * (1.0 - min(1.0, light * 1.2)) * effect * (1.0 - uPageLight);
  float alpha = clamp(max(base.a, max(glow.r, max(glow.g, glow.b))), 0.0, 1.0);
  float grain = fract(sin(dot(gl_FragCoord.xy + floor(uTime * 24.0), vec2(12.9898, 78.233))) * 43758.5453) - 0.5;
  vec3 color = base.rgb + glow + grain * 0.009 * base.a * clamp(light * 3.0 + 0.05, 0.05, 1.0) * effect * (1.0 - waveMask * 0.85);
  outColor = vec4(clamp(color, vec3(0.0), vec3(alpha)), alpha);
}
`;
    const noteLogo = `data:image/svg+xml;utf8,${encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 96 104"><path fill="#fff" d="M35 24c0-2.6 1.7-4.8 4.2-5.5L79.6 7.6C82.6 6.8 85 9 85 12.1V64c0 8.9-7.3 17-16.5 19.2C59.3 85.4 52 81 52 73.2c0-9 7.5-16.9 16.7-19.1 3.3-.8 6.3-.9 8.3-.2V28.8L43 38.1V77c0 8.9-7.3 17-16.5 19.2C17.3 98.4 10 94 10 86.2c0-9 7.5-16.9 16.7-19.1 3.3-.8 6.3-.9 8.3-.2V24Z"/></svg>')}`;
    const referenceMark = `data:image/svg+xml;utf8,${encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="#fff"><path d="M12.152 6.896c-.948 0-2.415-1.078-3.96-1.04-2.04.027-3.91 1.183-4.961 3.014-2.117 3.675-.546 9.103 1.519 12.09 1.013 1.454 2.208 3.09 3.792 3.039 1.52-.065 2.09-.987 3.935-.987 1.831 0 2.35.987 3.96.948 1.637-.026 2.676-1.48 3.676-2.948 1.156-1.688 1.636-3.325 1.662-3.415-.039-.013-3.182-1.221-3.22-4.857-.026-3.04 2.48-4.494 2.597-4.559-1.429-2.09-3.623-2.324-4.39-2.376-2-.156-3.675 1.09-4.61 1.09zM15.53 3.83c.843-1.012 1.4-2.427 1.245-3.83-1.207.052-2.662.805-3.532 1.818-.78.896-1.454 2.338-1.273 3.714 1.338.104 2.715-.688 3.559-1.701"/></svg>')}`;
    const liquidGlassLogoDefaults = {
        logo: noteLogo,
        mark: referenceMark,
        text: 'music',
        ending: 'logo',
        logoSize: 256,
        logoScale: 0.72,
        finalLogoScale: 1,
        tile: true,
        fontSize: 64,
        fontFamily: 'system-ui, -apple-system, "Helvetica Neue", Arial, sans-serif',
        fontWeight: 850,
        fontStretch: 'expanded',
        letterSpacing: -0.01,
        markGap: 0.12,
        theme: 'dark',
        logoColor: '#ffffff',
        logoRelief: 1,
        textColor: '#e2e6ee',
        tileColor: '#060c18',
        background: '#000000',
        depth: 1.2,
        dispersion: 1,
        relief: 1,
        caustics: 1,
        highlights: 1,
        bloom: 0.55,
        dropSize: 0.32,
        dropStrength: 1,
        echoTint: 0.5,
        innerWallFocus: 0,
        decay: 1.4,
        speed: 1,
        loop: false,
        loopDelay: 1.6,
        paused: false,
        maxDpr: 2,
    };
    const liquidGlassLogoNoteLogo = noteLogo;
    const liquidGlassLogoReferenceMark = referenceMark;
    /** Length of the scripted sequence at speed 1, in seconds. */
    const liquidGlassLogoDuration = 3.3;
    const logoFinishTime = 1.8;
    /** Smooth curve through [time, value] keys (Catmull-Rom with uneven spacing). */
    function track(keys, t) {
        if (t <= keys[0][0])
            return keys[0][1];
        const last = keys[keys.length - 1];
        if (t >= last[0])
            return last[1];
        let i = 0;
        while (t > keys[i + 1][0])
            i++;
        const [t1, v1] = keys[i];
        const [t2, v2] = keys[i + 1];
        const [t0, v0] = keys[i - 1] ?? [t1 - (t2 - t1), v1];
        const [t3, v3] = keys[i + 2] ?? [t2 + (t2 - t1), v2];
        const span = t2 - t1;
        const m1 = ((v2 - v0) / (t2 - t0)) * span;
        const m2 = ((v3 - v1) / (t3 - t1)) * span;
        const x = (t - t1) / span;
        const x2 = x * x;
        const x3 = x2 * x;
        return ((2 * x3 - 3 * x2 + 1) * v1 + (x3 - 2 * x2 + x) * m1 + (-2 * x3 + 3 * x2) * v2 + (x3 - x2) * m2);
    }
    // Size of the artwork under the glass relative to its resting size, measured from the
    // reference: magnified by the young drop, slowly receding as the glass settles, then
    // drawn into the second drop. Independent of the artwork itself.
    const artworkScale = [
        [0.12, 1.4],
        [0.33, 1.45],
        [0.46, 1.44],
        [0.63, 1.33],
        [0.79, 1.24],
        [0.96, 1.2],
        [1.13, 1.11],
        [1.25, 1.0],
        [1.335, 0.865],
        [1.376, 0.81],
        [1.418, 0.735],
        [1.46, 0.41],
    ];
    // The second drop, in wordmark half-widths (W) from the reference: its radius, the
    // radius of its magnifying core, how slowly its rim stretches the image (1 = not at
    // all), the core's center factor (negative inverts) and its edge magnification.
    const dropRadius = [
        [1.46, 0.32],
        [1.55, 0.36],
        [1.67, 0.49],
        [1.75, 0.52],
        [1.84, 0.56],
        [1.92, 0.68],
        [2.0, 0.88],
        [2.15, 0.95],
        [2.4, 1.06],
        [2.95, 1.3],
    ];
    const dropCore = [
        [1.46, 1.5],
        [1.84, 1.5],
        [1.92, 0.55],
        [2.0, 0.6],
        [2.09, 0.68],
        [2.17, 0.78],
        [2.3, 0.85],
        [2.5, 0.9],
        [2.75, 0.93],
        [2.95, 0.97],
        [3.02, 1.03],
    ];
    const dropStretch = [
        [1.46, 0.3],
        [1.84, 0.3],
        [2.0, 0.25],
        [2.1, 0.2],
        [2.2, 0.2],
        [2.4, 0.25],
        [2.7, 0.3],
        [2.85, 0.45],
        [2.95, 0.8],
        [3.02, 1],
    ];
    const dropCenter = [
        [1.46, -0.6],
        [1.6, -0.5],
        [1.75, -0.3],
        [1.8, 0.4],
        [1.84, 1],
    ];
    const dropMagnify = [
        [1.46, 1.5],
        [1.6, 1.5],
        [1.75, 2.2],
        [1.84, 1.4],
        [1.92, 1.2],
        [2.0, 1.05],
        [2.1, 1],
    ];
    const smooth = (from, to, value) => {
        const x = Math.min(1, Math.max(0, (value - from) / (to - from)));
        return x * x * (3 - 2 * x);
    };
    // Colors measured from the light and dark versions of the reference clip: a pale
    // gray page under a slightly cooler frosted plate with near-black ink, and the
    // original white ink on a deep blue-black plate.
    const liquidGlassLogoThemes = {
        dark: { logoColor: '#ffffff', textColor: '#e2e6ee', tileColor: '#060c18', background: '#000000' },
        light: {
            logoColor: '#09111b',
            textColor: '#09111b',
            tileColor: '#e4e6ea',
            background: '#e2e2e5',
        },
    };
    function normalizeLiquidGlassLogo(value) {
        const finite = (input, fallback, min, max) => Number.isFinite(input) ? Math.min(max, Math.max(min, input)) : fallback;
        const d = liquidGlassLogoDefaults;
        return {
            ...value,
            ending: value.ending === 'wordmark' ? 'wordmark' : 'logo',
            theme: value.theme === 'light' ? 'light' : 'dark',
            logoSize: finite(value.logoSize, d.logoSize, 24, 1024),
            logoScale: finite(value.logoScale, d.logoScale, 0.1, 1),
            finalLogoScale: finite(value.finalLogoScale, d.finalLogoScale, 0.1, 1),
            fontSize: finite(value.fontSize, d.fontSize, 8, 400),
            fontWeight: finite(value.fontWeight, d.fontWeight, 100, 1000),
            letterSpacing: finite(value.letterSpacing, d.letterSpacing, -0.3, 1),
            markGap: finite(value.markGap, d.markGap, 0, 2),
            depth: finite(value.depth, d.depth, 0, 6),
            dispersion: finite(value.dispersion, d.dispersion, 0, 4),
            relief: finite(value.relief, d.relief, 0, 3),
            caustics: finite(value.caustics, d.caustics, 0, 4),
            highlights: finite(value.highlights, d.highlights, 0, 4),
            bloom: finite(value.bloom, d.bloom, 0, 2),
            dropSize: finite(value.dropSize, d.dropSize, 0.05, 1.5),
            dropStrength: finite(value.dropStrength, d.dropStrength, 0, 4),
            echoTint: finite(value.echoTint, d.echoTint, 0, 1),
            logoRelief: finite(value.logoRelief, d.logoRelief, 0, 1),
            innerWallFocus: finite(value.innerWallFocus, d.innerWallFocus, 0, 1),
            decay: finite(value.decay, d.decay, 0.1, 10),
            speed: finite(value.speed, d.speed, 0.1, 4),
            loopDelay: finite(value.loopDelay, d.loopDelay, 0, 30),
            maxDpr: finite(value.maxDpr, d.maxDpr, 0.5, 4),
        };
    }
    function loadImage(src) {
        return new Promise((resolve) => {
            if (!src)
                return resolve(null);
            const image = new Image();
            image.crossOrigin = 'anonymous';
            image.decoding = 'async';
            image.onload = () => resolve(image);
            image.onerror = () => resolve(null);
            image.src = src;
        });
    }
    const nextPowerOfTwo = (value) => 2 ** Math.ceil(Math.log2(Math.max(2, value)));
    function drawTinted(context, image, x, y, width, height, color) {
        if (color === 'original') {
            context.drawImage(image, x, y, width, height);
            return;
        }
        const layer = document.createElement('canvas');
        layer.width = Math.max(1, Math.ceil(width));
        layer.height = Math.max(1, Math.ceil(height));
        const ink = layer.getContext('2d');
        ink.drawImage(image, 0, 0, layer.width, layer.height);
        ink.globalCompositeOperation = 'source-in';
        ink.fillStyle = color;
        ink.fillRect(0, 0, layer.width, layer.height);
        context.drawImage(layer, x, y, width, height);
    }
    function imageSize(image) {
        const width = image.naturalWidth || image.width || 1;
        const height = image.naturalHeight || image.height || 1;
        return { width, height };
    }
    // A shallow relief follows the uploaded alpha silhouette, rather than any hard-coded
    // glyph. A distance transform supplies a rounded shoulder for the diffuse studio light.
    function shadeLogo(canvas, radius, strength) {
        if (strength <= 0)
            return;
        const context = canvas.getContext('2d');
        const { width, height } = canvas;
        const image = context.getImageData(0, 0, width, height);
        const pixels = image.data;
        const distance = new Float32Array(width * height);
        for (let i = 0; i < distance.length; i++)
            distance[i] = pixels[i * 4 + 3] > 127 ? 1e6 : 0;
        const diagonal = Math.SQRT2;
        for (let y = 1; y < height - 1; y++) {
            for (let x = 1; x < width - 1; x++) {
                const i = y * width + x;
                distance[i] = Math.min(distance[i], distance[i - 1] + 1, distance[i - width] + 1, distance[i - width - 1] + diagonal, distance[i - width + 1] + diagonal);
            }
        }
        for (let y = height - 2; y > 0; y--) {
            for (let x = width - 2; x > 0; x--) {
                const i = y * width + x;
                distance[i] = Math.min(distance[i], distance[i + 1] + 1, distance[i + width] + 1, distance[i + width - 1] + diagonal, distance[i + width + 1] + diagonal);
            }
        }
        for (let i = 0; i < distance.length; i++) {
            const q = Math.max(0, 1 - distance[i] / radius);
            distance[i] = Math.sqrt(Math.max(0, 1 - q * q)) * radius * 0.4;
        }
        for (let y = 1; y < height - 1; y++) {
            for (let x = 1; x < width - 1; x++) {
                const i = y * width + x;
                if (!pixels[i * 4 + 3])
                    continue;
                const nx = (distance[i - 1] - distance[i + 1]) * 0.5;
                const ny = (distance[i - width] - distance[i + width]) * 0.5;
                const diffuse = Math.max(0, (-nx * 0.45 - ny * 0.6 + 0.8) / Math.hypot(nx, ny, 1));
                const shaded = (0.74 + diffuse * 0.26) / (0.74 + 0.8 * 0.26);
                const light = 1 + (shaded - 1) * strength;
                for (let c = 0; c < 3; c++)
                    pixels[i * 4 + c] *= light;
            }
        }
        context.putImageData(image, 0, 0);
    }
    // The logo fills a square box one plate edge wide.
    function rasterLogo(image, tuning, dpr) {
        const size = Math.min(2048, nextPowerOfTwo(tuning.logoSize * dpr * 2));
        const canvas = document.createElement('canvas');
        canvas.width = canvas.height = size;
        const context = canvas.getContext('2d');
        if (image) {
            const { width, height } = imageSize(image);
            const fit = (size * tuning.logoScale) / Math.max(width, height);
            const w = width * fit;
            const h = height * fit;
            drawTinted(context, image, (size - w) / 2, (size - h) / 2, w, h, tuning.logoColor);
        }
        if (image && tuning.logoColor !== 'original') {
            shadeLogo(canvas, canvas.width * 0.03, tuning.logoRelief);
        }
        return { canvas, box: [tuning.logoSize, tuning.logoSize] };
    }
    function font(tuning) {
        return `${tuning.fontWeight} ${tuning.fontSize}px ${tuning.fontFamily}`;
    }
    // The wordmark box is padded so refracted rays sample transparent texels past its edges.
    function rasterText(mark, tuning, dpr) {
        const probe = document.createElement('canvas').getContext('2d');
        probe.font = font(tuning);
        probe.fontStretch = tuning.fontStretch;
        const spacing = tuning.letterSpacing * tuning.fontSize;
        probe.letterSpacing = `${spacing}px`;
        // Shape the whole string: per-character drawing loses kerning, ligatures and Arabic joining.
        const textWidth = Math.max(0, probe.measureText(tuning.text).width);
        const metrics = probe.measureText(tuning.text || 'x');
        const ascent = metrics.actualBoundingBoxAscent || tuning.fontSize * 0.72;
        const descent = metrics.actualBoundingBoxDescent || tuning.fontSize * 0.2;
        const xHeight = probe.measureText('x').actualBoundingBoxAscent || tuning.fontSize * 0.52;
        let markWidth = 0;
        let markHeight = 0;
        if (mark) {
            const { width, height } = imageSize(mark);
            markHeight = Math.max(ascent, xHeight * 1.3);
            markWidth = (width / height) * markHeight;
        }
        const gap = mark && tuning.text.length ? tuning.markGap * tuning.fontSize : 0;
        const contentWidth = markWidth + gap + textWidth;
        const pad = tuning.fontSize * 0.5;
        const box = [contentWidth + pad * 2, tuning.fontSize * 2];
        const scale = Math.min(dpr * 2, 4096 / box[0], 4096 / box[1]);
        const canvas = document.createElement('canvas');
        canvas.width = Math.min(4096, nextPowerOfTwo(box[0] * scale));
        canvas.height = Math.min(4096, nextPowerOfTwo(box[1] * scale));
        const context = canvas.getContext('2d');
        context.scale(canvas.width / box[0], canvas.height / box[1]);
        // Center the ink box vertically.
        const baseline = box[1] / 2 + (ascent - descent) / 2;
        let x = pad;
        if (mark) {
            const color = tuning.logoColor === 'original' ? 'original' : tuning.textColor;
            drawTinted(context, mark, x, baseline - markHeight, markWidth, markHeight, color);
            x += markWidth + gap;
        }
        context.font = font(tuning);
        context.fontStretch = tuning.fontStretch;
        context.fillStyle = tuning.textColor;
        context.textBaseline = 'alphabetic';
        context.letterSpacing = `${spacing}px`;
        context.textAlign = 'left';
        context.fillText(tuning.text, x, baseline);
        if (tuning.logoColor !== 'original') {
            shadeLogo(canvas, (tuning.fontSize * 0.04 * canvas.height) / box[1], tuning.logoRelief);
        }
        return { canvas, box, contentWidth };
    }
    function parseColor(value) {
        const probe = document.createElement('canvas').getContext('2d');
        probe.fillStyle = '#000';
        probe.fillStyle = value;
        const hex = String(probe.fillStyle);
        if (hex.startsWith('#')) {
            const n = parseInt(hex.slice(1, 7), 16);
            return [(n >> 16) / 255, ((n >> 8) & 255) / 255, (n & 255) / 255];
        }
        const parts = hex.match(/[\d.]+/g)?.map(Number) ?? [0, 0, 0];
        return [parts[0] / 255, parts[1] / 255, parts[2] / 255];
    }
    function createLiquidGlassLogoRenderer(canvas, initial, onReady) {
        const gl = canvas.getContext('webgl2', {
            premultipliedAlpha: true,
            alpha: true,
            antialias: false,
        });
        if (!gl)
            return null;
        const compile = (type, source) => {
            const shader = gl.createShader(type);
            gl.shaderSource(shader, source);
            gl.compileShader(shader);
            if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
                const message = gl.getShaderInfoLog(shader);
                gl.deleteShader(shader);
                throw new Error(message ?? 'Liquid Glass Logo shader failed to compile.');
            }
            return shader;
        };
        const link = (fragment) => {
            const result = gl.createProgram();
            const vertex = compile(gl.VERTEX_SHADER, liquidGlassLogoVertex);
            const pixel = compile(gl.FRAGMENT_SHADER, fragment);
            gl.attachShader(result, vertex);
            gl.attachShader(result, pixel);
            gl.bindAttribLocation(result, 0, 'aPosition');
            gl.linkProgram(result);
            gl.deleteShader(vertex);
            gl.deleteShader(pixel);
            if (!gl.getProgramParameter(result, gl.LINK_STATUS)) {
                const message = gl.getProgramInfoLog(result);
                gl.deleteProgram(result);
                throw new Error(message ?? 'Liquid Glass Logo program failed to link.');
            }
            return result;
        };
        const program = link(liquidGlassLogoRender);
        const composite = link(liquidGlassLogoComposite);
        const compositeUniform = Object.fromEntries([
            'uScene',
            'uResolution',
            'uDpr',
            'uUnit',
            'uSettle',
            'uBloom',
            'uTime',
            'uFinish',
            'uPageLight',
        ].map((name) => [name, gl.getUniformLocation(composite, name)]));
        const locations = new Map();
        const uniform = (name) => {
            if (!locations.has(name))
                locations.set(name, gl.getUniformLocation(program, name));
            return locations.get(name);
        };
        const quad = gl.createVertexArray();
        gl.bindVertexArray(quad);
        const buffer = gl.createBuffer();
        gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
        gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 3, -1, -1, 3]), gl.STATIC_DRAW);
        gl.enableVertexAttribArray(0);
        gl.vertexAttribPointer(0, 2, gl.FLOAT, false, 0, 0);
        const texture = () => {
            const value = gl.createTexture();
            gl.bindTexture(gl.TEXTURE_2D, value);
            gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, 1, 1, 0, gl.RGBA, gl.UNSIGNED_BYTE, new Uint8Array(4));
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR_MIPMAP_LINEAR);
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
            return value;
        };
        const logoTexture = texture();
        const textTexture = texture();
        const sceneTexture = texture();
        const sceneFramebuffer = gl.createFramebuffer();
        gl.bindFramebuffer(gl.FRAMEBUFFER, sceneFramebuffer);
        gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, sceneTexture, 0);
        gl.bindFramebuffer(gl.FRAMEBUFFER, null);
        let sceneWidth = 0;
        let sceneHeight = 0;
        const resizeScene = () => {
            if (sceneWidth === canvas.width && sceneHeight === canvas.height)
                return;
            sceneWidth = canvas.width;
            sceneHeight = canvas.height;
            gl.activeTexture(gl.TEXTURE0);
            gl.bindTexture(gl.TEXTURE_2D, sceneTexture);
            gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, sceneWidth, sceneHeight, 0, gl.RGBA, gl.UNSIGNED_BYTE, null);
        };
        const upload = (value, source) => {
            gl.activeTexture(gl.TEXTURE0);
            gl.bindTexture(gl.TEXTURE_2D, value);
            gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, true);
            gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, true);
            gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, source);
            gl.generateMipmap(gl.TEXTURE_2D);
        };
        let tuning = initial;
        let dpr = 1;
        let width = 1;
        let height = 1;
        let textBox = [1, 1];
        let textWidth = 0;
        let loaded = false;
        let disposed = false;
        let rasterKey = '';
        let rasterVersion = 0;
        let logoCache = null;
        let clock = 0;
        let last = 0;
        let playing = !initial.paused;
        let visible = true;
        let frame = 0;
        const reduced = window.matchMedia?.('(prefers-reduced-motion: reduce)');
        const fit = () => Math.min(1, 
        // Reserve room for the side reflections as they travel away from the plate.
        width / Math.max(tuning.logoSize * 1.9, tuning.ending === 'wordmark' ? textWidth * 1.22 : 0), height / (tuning.logoSize * 1.5));
        const draw = () => {
            if (!loaded || disposed)
                return;
            const scale = fit();
            resizeScene();
            gl.bindFramebuffer(gl.FRAMEBUFFER, sceneFramebuffer);
            gl.viewport(0, 0, canvas.width, canvas.height);
            gl.useProgram(program);
            gl.bindVertexArray(quad);
            [logoTexture, textTexture].forEach((value, index) => {
                gl.activeTexture(gl.TEXTURE0 + index);
                gl.bindTexture(gl.TEXTURE_2D, value);
            });
            gl.uniform1i(uniform('uLogo'), 0);
            gl.uniform1i(uniform('uText'), 1);
            gl.uniform2f(uniform('uResolution'), canvas.width, canvas.height);
            gl.uniform1f(uniform('uDpr'), canvas.width / width);
            gl.uniform1f(uniform('uUnit'), tuning.logoSize * scale);
            // The logo always occupies one plate edge. Its bounds must follow the
            // current size immediately, even while a sharper texture loads asynchronously.
            gl.uniform2f(uniform('uLogoBox'), tuning.logoSize * scale, tuning.logoSize * scale);
            gl.uniform2f(uniform('uTextBox'), textBox[0] * scale, textBox[1] * scale);
            gl.uniform1f(uniform('uTextWidth'), textWidth * scale);
            const finishTime = tuning.ending === 'logo' ? logoFinishTime : liquidGlassLogoDuration;
            const sequenceTime = reduced?.matches ? finishTime : clock;
            const displayTime = tuning.ending === 'logo' ? Math.min(logoFinishTime, sequenceTime) : sequenceTime;
            gl.uniform1f(uniform('uTime'), displayTime);
            gl.uniform1f(uniform('uWordmarkEnabled'), tuning.ending === 'wordmark' ? 1 : 0);
            const settle = tuning.ending === 'logo' ? Math.max(0, Math.min(1, (sequenceTime - 1.16) / (1.4 - 1.16))) : 0;
            // The wordmark drop scales with the measured wordmark so any text gets the same optics.
            const unitPx = tuning.logoSize * scale;
            const reach = Math.max((textWidth * scale) / 2, unitPx * 0.3);
            const t = displayTime;
            const settleWaves = 1 - smooth(2.85, 3.02, t);
            gl.uniform4f(uniform('uDrop'), reach * track(dropRadius, t), reach * track(dropCore, t), Math.max(0.02, Math.min(1, track(dropStretch, t))), 1 + (track(dropCenter, t) - 1) * tuning.dropStrength);
            gl.uniform4f(uniform('uDropLens'), 1 + (track(dropMagnify, t) - 1) * tuning.dropStrength, 2 + 4 * smooth(1.74, 1.84, t), unitPx *
                (0.009 * Math.exp(-Math.max(t - 1.46, 0) / 0.5) * (1 - smooth(1.85, 1.95, t)) +
                    0.012 * smooth(2.2, 2.4, t)) *
                settleWaves, unitPx * (0.07 + (0.03 - 0.07) * smooth(1.9, 2.4, t)));
            gl.uniform4f(uniform('uDropWave'), (t - 1.45) * 42, 1 - smooth(1.85, 2.0, t), 2 * (0.12 + 1.6 * smooth(1.86, 2.0, t) - 0.9 * smooth(2.25, 2.5, t)) * settleWaves, 0);
            const artworkSettle = smooth(1.13, 1.28, sequenceTime);
            const artScale = tuning.ending === 'logo' && sequenceTime > 1.13
                ? track(artworkScale, 1.13) * (1 - artworkSettle) + tuning.finalLogoScale * artworkSettle
                : track(artworkScale, displayTime);
            gl.uniform1f(uniform('uArtScale'), artScale);
            // The logo reaches its resting size first, then holds while the glass clears.
            gl.uniform1f(uniform('uSettle'), settle);
            gl.uniform3fv(uniform('uTile'), parseColor(tuning.tileColor));
            const [pageR, pageG, pageB] = parseColor(tuning.background);
            const pageLuma = 0.299 * pageR + 0.587 * pageG + 0.114 * pageB;
            const pageLight = Math.min(1, Math.max(0, (pageLuma - 0.35) / 0.4));
            gl.uniform1f(uniform('uPageLight'), pageLight);
            gl.uniform1f(uniform('uTileEnabled'), tuning.tile ? 1 : 0);
            gl.uniform1f(uniform('uDepth'), tuning.depth);
            gl.uniform1f(uniform('uDispersion'), tuning.dispersion);
            gl.uniform1f(uniform('uRelief'), tuning.relief);
            gl.uniform1f(uniform('uCaustics'), tuning.caustics);
            gl.uniform1f(uniform('uHighlights'), tuning.highlights);
            gl.uniform1f(uniform('uEchoTint'), tuning.echoTint);
            gl.uniform1f(uniform('uInnerWallFocus'), tuning.innerWallFocus);
            gl.uniform1f(uniform('uDropSize'), tuning.dropSize);
            gl.uniform1f(uniform('uDropStrength'), tuning.dropStrength);
            gl.uniform1f(uniform('uDecay'), tuning.decay);
            gl.drawArrays(gl.TRIANGLES, 0, 3);
            // A mip pyramid supplies soft light at several scales without a costly blur kernel.
            gl.bindFramebuffer(gl.FRAMEBUFFER, null);
            gl.useProgram(composite);
            gl.activeTexture(gl.TEXTURE0);
            gl.bindTexture(gl.TEXTURE_2D, sceneTexture);
            gl.generateMipmap(gl.TEXTURE_2D);
            gl.uniform1i(compositeUniform.uScene, 0);
            gl.uniform2f(compositeUniform.uResolution, canvas.width, canvas.height);
            gl.uniform1f(compositeUniform.uDpr, canvas.width / width);
            gl.uniform1f(compositeUniform.uUnit, unitPx);
            gl.uniform1f(compositeUniform.uSettle, settle);
            gl.uniform1f(compositeUniform.uBloom, tuning.bloom);
            gl.uniform1f(compositeUniform.uPageLight, pageLight);
            gl.uniform1f(compositeUniform.uTime, displayTime);
            gl.uniform1f(compositeUniform.uFinish, tuning.ending === 'logo'
                ? Math.max(0, Math.min(1, (sequenceTime - 1.05) / (logoFinishTime - 1.05)))
                : Math.max(0, Math.min(1, (sequenceTime - 2.8) / (liquidGlassLogoDuration - 2.8))));
            gl.drawArrays(gl.TRIANGLES, 0, 3);
        };
        const raster = async () => {
            const key = JSON.stringify([
                tuning.logo,
                tuning.mark,
                tuning.text,
                tuning.logoSize,
                tuning.logoScale,
                tuning.fontSize,
                tuning.fontFamily,
                tuning.fontWeight,
                tuning.fontStretch,
                tuning.letterSpacing,
                tuning.markGap,
                tuning.logoColor,
                tuning.logoRelief,
                tuning.textColor,
                dpr,
            ]);
            if (key === rasterKey)
                return;
            rasterKey = key;
            const version = ++rasterVersion;
            // Snapshot the options: a late image or font load must never rasterize newer props.
            const options = { ...tuning };
            const markSource = options.mark === 'logo' ? options.logo : options.mark;
            const [logo, mark] = await Promise.all([
                loadImage(options.logo),
                markSource === options.logo ? null : loadImage(markSource),
                document.fonts?.load(font(options), options.text || 'x').catch(() => undefined),
            ]);
            if (disposed || version !== rasterVersion)
                return;
            const markImage = markSource === options.logo ? logo : mark;
            const logoKey = JSON.stringify([
                options.logo,
                options.logoSize,
                options.logoScale,
                options.logoColor,
                options.logoRelief,
                dpr,
            ]);
            const logoRaster = logoCache?.key === logoKey ? logoCache.raster : rasterLogo(logo, options, dpr);
            logoCache = { key: logoKey, raster: logoRaster };
            const textRaster = rasterText(markSource ? markImage : null, options, dpr);
            upload(logoTexture, logoRaster.canvas);
            upload(textTexture, textRaster.canvas);
            textBox = textRaster.box;
            textWidth = textRaster.contentWidth;
            if (!loaded) {
                loaded = true;
                last = 0;
                onReady?.();
            }
            draw();
            request();
        };
        const tick = (now) => {
            frame = 0;
            const delta = last ? Math.min(0.05, (now - last) / 1000) : 0;
            last = now;
            const moving = playing && visible && !reduced?.matches && loaded;
            if (moving) {
                clock += delta * tuning.speed;
                const cycle = (tuning.ending === 'logo' ? logoFinishTime : liquidGlassLogoDuration) + tuning.loopDelay;
                if (tuning.loop && clock >= cycle)
                    clock %= cycle;
            }
            draw();
            if (moving &&
                (tuning.loop || clock < (tuning.ending === 'logo' ? logoFinishTime : liquidGlassLogoDuration)))
                request();
            else
                last = 0;
        };
        function request() {
            if (!frame && !disposed)
                frame = requestAnimationFrame(tick);
        }
        const resize = () => {
            // CSS transforms can move and scale the preview without changing its layout.
            // Keep WebGL coordinates tied to that layout throughout the animation.
            const style = getComputedStyle(canvas);
            width = Math.max(1, Number.parseFloat(style.width) || canvas.clientWidth);
            height = Math.max(1, Number.parseFloat(style.height) || canvas.clientHeight);
            const nextDpr = Math.min(window.devicePixelRatio || 1, tuning.maxDpr);
            canvas.width = Math.max(1, Math.round(width * nextDpr));
            canvas.height = Math.max(1, Math.round(height * nextDpr));
            if (nextDpr !== dpr) {
                dpr = nextDpr;
                void raster();
            }
            draw();
            request();
        };
        const resizeObserver = new ResizeObserver(resize);
        resizeObserver.observe(canvas);
        const visibility = new IntersectionObserver(([entry]) => {
            visible = entry?.isIntersecting ?? true;
            last = 0;
            if (visible)
                request();
        });
        visibility.observe(canvas);
        const onMotion = () => {
            last = 0;
            request();
        };
        reduced?.addEventListener?.('change', onMotion);
        resize();
        void raster();
        return {
            update(next) {
                const wasPlaying = playing;
                tuning = next;
                playing = !next.paused;
                if (!wasPlaying && playing)
                    last = 0;
                resize();
                void raster();
            },
            replay() {
                clock = 0;
                last = 0;
                draw();
                request();
            },
            seek(seconds) {
                clock = Number.isFinite(seconds) ? Math.max(0, Math.min(3600, seconds)) : 0;
                last = 0;
                draw();
            },
            pause() {
                playing = false;
                last = 0;
            },
            play() {
                playing = true;
                last = 0;
                request();
            },
            dispose() {
                disposed = true;
                logoCache = null;
                cancelAnimationFrame(frame);
                resizeObserver.disconnect();
                visibility.disconnect();
                reduced?.removeEventListener?.('change', onMotion);
                gl.deleteTexture(logoTexture);
                gl.deleteTexture(textTexture);
                gl.deleteTexture(sceneTexture);
                gl.deleteFramebuffer(sceneFramebuffer);
                gl.deleteBuffer(buffer);
                gl.deleteVertexArray(quad);
                gl.deleteProgram(program);
                gl.deleteProgram(composite);
            },
        };
    }
    function mountLiquidGlassLogo(container, props = {
        logo: '/logo.svg',
        ending: 'logo',
        height: 480,
    }) {
        const { height = 480, style, ...overrides } = props;
        const theme = liquidGlassLogoThemes[overrides.theme === 'light' ? 'light' : 'dark'];
        const tuning = normalizeLiquidGlassLogo({
            ...liquidGlassLogoDefaults,
            ...theme,
            logo: '/logo.svg',
            ending: 'logo',
            ...overrides,
        });
        container.classList.add('liquid-glass-logo');
        container.style.height = typeof height === 'number' ? `${height}px` : String(height);
        container.style.background = tuning.background;
        if (style) {
            Object.assign(container.style, style);
        }
        container.setAttribute('role', 'img');
        container.setAttribute('aria-label', tuning.ending === 'logo' ? 'Logo' : tuning.text);
        let canvas = container.querySelector('canvas');
        if (!canvas) {
            canvas = document.createElement('canvas');
            canvas.setAttribute('aria-hidden', 'true');
            container.appendChild(canvas);
        }
        const fallbackEl = container.querySelector('.liquid-glass-logo-fallback');
        let renderer = null;
        try {
            renderer = createLiquidGlassLogoRenderer(canvas, tuning, () => {
                container.setAttribute('data-liquid-glass-ready', 'true');
                if (fallbackEl)
                    fallbackEl.style.display = 'none';
            });
        }
        catch {
            renderer = null;
        }
        if (!renderer) {
            if (fallbackEl) {
                fallbackEl.style.display = 'flex';
            }
            else {
                const span = document.createElement('span');
                span.className = 'liquid-glass-logo-fallback';
                span.setAttribute('aria-hidden', 'true');
                span.style.color = tuning.textColor;
                span.style.fontFamily = tuning.fontFamily;
                span.style.fontWeight = String(tuning.fontWeight);
                span.style.fontSize = `${tuning.fontSize}px`;
                span.style.letterSpacing = `${tuning.letterSpacing}em`;
                span.style.gap = `${tuning.markGap}em`;
                const img = document.createElement('img');
                img.src = tuning.logo;
                img.alt = '';
                const edge = tuning.logoSize * tuning.logoScale * tuning.finalLogoScale;
                img.style.width = `${edge}px`;
                img.style.height = `${edge}px`;
                img.style.maxWidth = '80%';
                img.style.maxHeight = '80%';
                img.style.objectFit = 'contain';
                span.appendChild(img);
                container.appendChild(span);
            }
        }
        return renderer;
    }
    window.LiquidGlassLogo = {
        mount: mountLiquidGlassLogo,
        createLiquidGlassLogoRenderer,
        liquidGlassLogoDefaults,
        liquidGlassLogoThemes,
        normalizeLiquidGlassLogo,
        liquidGlassLogoDuration,
    };
})();
