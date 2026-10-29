"""Derive transparent Rising Slash VFX from the approved effect masters.

Fragments are angular shards of the same shield image, so the emblem,
wood grain and gold ring stay continuous. Echoes are alpha variants of
the slash and wave masters, not separately invented drawings.
"""
import os
import uuid
import numpy as np
from PIL import Image, ImageFilter

SRC = os.path.join(os.path.expanduser("~"), ".cursor", "projects", "d-Workplace-Gaming-K4RMA", "assets")
OUT = os.path.join("Assets", "K4RMA", "Resources", "K4RMAArtwork", "RisingSlash")

MASTERS = {
    "Slash": "rising-slash-master.jpg",
    "Impact": "rising-impact-master.jpg",
    "Wave": "rising-wave-master.jpg",
    "Shield": "rising-shield-master.jpg",
    "Charge": "rising-charge-master.jpg",
    "Parry": "rising-parry-master.jpg",
}


def key_black(path):
    rgb = np.asarray(Image.open(path).convert("RGB"), dtype=np.float32)
    peak = rgb.max(axis=2)
    alpha = np.clip((peak - 10.0) * (255.0 / 32.0), 0, 255).astype(np.uint8)
    out = np.dstack([rgb.astype(np.uint8), alpha])
    return out


def crop(rgba, pad=8):
    alpha = rgba[:, :, 3]
    ys, xs = np.where(alpha > 16)
    if len(xs) == 0:
        return rgba
    y0, y1 = max(0, ys.min() - pad), min(rgba.shape[0], ys.max() + pad + 1)
    x0, x1 = max(0, xs.min() - pad), min(rgba.shape[1], xs.max() + pad + 1)
    return rgba[y0:y1, x0:x1]


def save_png(rgba, path):
    Image.fromarray(rgba, "RGBA").save(path, "PNG")


def echo(rgba, alpha_scale):
    copy = rgba.copy()
    copy[:, :, 3] = (copy[:, :, 3].astype(np.float32) * alpha_scale).astype(np.uint8)
    blurred = Image.fromarray(copy, "RGBA").filter(ImageFilter.GaussianBlur(1.2))
    return np.asarray(blurred)


def shards(shield):
    h, w = shield.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w]
    cy, cx = (h - 1) / 2.0, (w - 1) / 2.0
    ang = np.arctan2(yy - cy, xx - cx)
    rad = np.hypot(yy - cy, xx - cx)
    pieces = []
    for i in range(8):
        start = -np.pi + i * (2 * np.pi / 8)
        jitter = 0.22 * np.sin(rad * 0.07 + i * 1.37)
        delta = np.mod(ang - start, 2 * np.pi)
        width = (2 * np.pi / 8) + jitter
        keep = (delta >= 0) & (delta <= width) & (shield[:, :, 3] > 18)
        piece = np.zeros_like(shield)
        piece[keep] = shield[keep]
        edge = keep & ((delta < 0.09) | (delta > width - 0.09))
        piece[edge, :3] = (piece[edge, :3].astype(np.float32) * 0.42).astype(np.uint8)
        pieces.append(crop(piece, 4))
    return pieces


def meta(name, width, height, guid):
    sprite_id = uuid.uuid4().hex
    return f"""fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 1
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationMethod: 0
  spriteTessellationDetail: -1
  spriteGeometrySubdivision: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 1
  swizzle: 50462976
  cookieLightType: 2
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites:
    - serializedVersion: 2
      name: {name}
      rect:
        serializedVersion: 2
        x: 0
        y: 0
        width: {width}
        height: {height}
      alignment: 0
      pivot: {{x: 0.5, y: 0.5}}
      border: {{x: 0, y: 0, z: 0, w: 0}}
      customData: 
      outline: []
      physicsShape: []
      tessellationDetail: -1
      bones: []
      spriteID: {sprite_id}
      internalID: 21300000
      vertices: []
      indices: 
      edges: []
      weights: []
    outline: []
    customData: 
    physicsShape: []
    bones: []
    spriteID: 
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable:
      {name}: 21300000
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def main():
    os.makedirs(OUT, exist_ok=True)
    keyed = {name: crop(key_black(os.path.join(SRC, file))) for name, file in MASTERS.items()}
    keyed["SlashEcho"] = crop(echo(keyed["Slash"], 0.62))
    keyed["WaveEcho"] = crop(echo(keyed["Wave"], 0.55))
    for i, piece in enumerate(shards(keyed["Shield"])):
        keyed[f"Fragment{i}"] = piece
    for name, rgba in keyed.items():
        path = os.path.join(OUT, name + ".png")
        save_png(rgba, path)
        guid = uuid.uuid4().hex
        with open(path + ".meta", "w", encoding="utf-8", newline="\n") as handle:
            handle.write(meta(name, rgba.shape[1], rgba.shape[0], guid))
        print(f"{name} {rgba.shape[1]}x{rgba.shape[0]}")


if __name__ == "__main__":
    main()
