$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$art = Join-Path $repo 'Assets/K4RMA/Art/Resources/K4RMAArtwork/Effects/Sword'
$output = Join-Path $repo 'output/validation/sword-overhaul-20261102'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public sealed class SwordImageStats {
    public string Name; public int Width, Height, CoreX, CoreY, CoreWidth, CoreHeight;
    public int TransparentPixels, OpaquePixels; public double PivotX, PivotY;
}
public static class SwordImagePreparation {
    static byte[] Pixels(Bitmap image) {
        var data=image.LockBits(new Rectangle(0,0,image.Width,image.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        try { var bytes=new byte[image.Width*image.Height*4];
            for(int y=0;y<image.Height;y++) Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),bytes,y*image.Width*4,image.Width*4);
            return bytes;
        } finally { image.UnlockBits(data); }
    }
    public static int Split(string full,string upper,string lower) {
        using(var image=new Bitmap(full)) {
            if(image.Height%2!=0) throw new Exception("The full image must have an even height");
            int half=image.Height/2;
            using(var top=image.Clone(new Rectangle(0,0,image.Width,half),PixelFormat.Format32bppArgb)) top.Save(upper,ImageFormat.Png);
            using(var bottom=image.Clone(new Rectangle(0,half,image.Width,half),PixelFormat.Format32bppArgb)) bottom.Save(lower,ImageFormat.Png);
            byte[] source=Pixels(image);
            using(var top=new Bitmap(upper)) using(var bottom=new Bitmap(lower)) {
                byte[] a=Pixels(top), b=Pixels(bottom); int mismatches=0;
                for(int i=0;i<source.Length;i++) if(source[i]!=(i<a.Length?a[i]:b[i-a.Length])) mismatches++;
                if(mismatches!=0) throw new Exception("Half recombination differs: "+mismatches+" RGBA bytes");
                return mismatches;
            }
        }
    }
    public static SwordImageStats Analyze(string path) {
        using(var image=new Bitmap(path)) {
            var result=new SwordImageStats {Name=System.IO.Path.GetFileNameWithoutExtension(path),Width=image.Width,Height=image.Height};
            byte[] pixels=Pixels(image); int left=image.Width,right=-1,top=image.Height,bottom=-1;
            for(int y=0;y<image.Height;y++) for(int x=0;x<image.Width;x++) {
                int alpha=pixels[(y*image.Width+x)*4+3];
                if(alpha==0) result.TransparentPixels++;
                if(alpha>=128) { result.OpaquePixels++; left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y); }
            }
            if(result.TransparentPixels==0||right<left) throw new Exception("Missing transparency or cutting core: "+path);
            result.CoreX=left;result.CoreY=image.Height-1-bottom;result.CoreWidth=right-left+1;result.CoreHeight=bottom-top+1;
            result.PivotX=(left+result.CoreWidth/2.0)/image.Width;result.PivotY=(result.CoreY+result.CoreHeight/2.0)/image.Height;
            return result;
        }
    }
}
'@
# The user requested exact upper/lower pixel cuts, rather than regenerated variants.
$mismatches = [SwordImagePreparation]::Split((Join-Path $art 'SW02_AimedSlash_v2.png'),
    (Join-Path $art 'SW01_AimedSlash_UpperHalf_v2.png'), (Join-Path $art 'SW01_AimedSlash_LowerHalf_v2.png'))
$names = @('SW02_AimedSlash_v2','SW01_AimedSlash_UpperHalf_v2','SW01_AimedSlash_LowerHalf_v2',
    'SW03_GroundScar_v5','SW03_RisingBlade_v5','SW03_HitFlash_v5')
$stats = foreach ($name in $names) {
    $record = [SwordImagePreparation]::Analyze((Join-Path $art ($name + '.png')))
    $metaPath = Join-Path $art ($name + '.png.meta')
    $guid = if (Test-Path -LiteralPath $metaPath) {
        [regex]::Match([IO.File]::ReadAllText($metaPath),'(?m)^guid: ([a-f0-9]+)').Groups[1].Value
    } else { [guid]::NewGuid().ToString('N') }
    $px = $record.PivotX.ToString('R',[Globalization.CultureInfo]::InvariantCulture)
    $py = $record.PivotY.ToString('R',[Globalization.CultureInfo]::InvariantCulture)
    $meta = @"
fileFormatVersion: 2
guid: $guid
TextureImporter:
  externalObjects: {}
  serializedVersion: 13
  mipmaps:
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 0
  textureFormat: 1
  maxTextureSize: 4096
  textureSettings:
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 9
  spritePivot: {x: $px, y: $py}
  spritePixelsToUnits: 100
  alphaUsage: 1
  alphaIsTransparency: 1
  textureType: 8
  textureShape: 1
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 4096
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 100
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {}
  userData:
  assetBundleName:
  assetBundleVariant:
"@
    [IO.File]::WriteAllText($metaPath,$meta + "`n",[Text.UTF8Encoding]::new($false))
    $record
}
$stats | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $output 'image-stats.json') -Encoding UTF8
[pscustomobject]@{operation='exact horizontal bisection, no resize';rgbaByteMismatches=$mismatches;images=$stats} |
    ConvertTo-Json -Depth 5 | Set-Content (Join-Path $output 'image-validation.json') -Encoding UTF8
$stats | Format-Table Name,Width,Height,CoreX,CoreY,CoreWidth,CoreHeight,TransparentPixels
Write-Output "PASS: exact half recombination; $mismatches differing RGBA bytes"
