#ifndef UNIVERSAL_CLUSTERING_INCLUDED
#define UNIVERSAL_CLUSTERING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"

#if USE_CLUSTERED_LIGHTING

// Match with values in UniversalRenderPipeline.cs
#define MAX_ZBIN_VEC4S 1024
#if MAX_VISIBLE_LIGHTS <= 32
    #define MAX_LIGHTS_PER_TILE 32
    #define MAX_TILE_VEC4S 1024
#else
    #define MAX_LIGHTS_PER_TILE MAX_VISIBLE_LIGHTS
    #define MAX_TILE_VEC4S 4096
#endif

#define MAX_REFLECTION_PROBES (min(MAX_VISIBLE_LIGHTS, 64))

float4 _FPParams0;
float4 _FPParams1;

#define URP_FP_ZBIN_SCALE (_FPParams0.x)
#define URP_FP_ZBIN_OFFSET (_FPParams0.y)
#define URP_FP_PROBES_BEGIN ((uint)_FPParams0.z)
// Directional lights would be in all clusters, so they don't go into the cluster structure.
// Instead, they are stored first in the light buffer.
#define URP_FP_DIRECTIONAL_LIGHTS_COUNT ((uint)_FPParams0.w)

// Scale from screen-space UV [0, 1] to tile coordinates [0, tile resolution].
#define URP_FP_TILE_SCALE ((float2)_FPParams1.xy)
#define URP_FP_TILE_COUNT_X ((uint)_FPParams1.z)
#define URP_FP_WORDS_PER_TILE ((uint)_FPParams1.w)

CBUFFER_START(URP_ZBinBuffer)
        float4 URP_ZBins[MAX_ZBIN_VEC4S];
CBUFFER_END
CBUFFER_START(URP_TileBuffer)
        float4 URP_Tiles[MAX_TILE_VEC4S];
CBUFFER_END

TEXTURE2D(URP_ReflProbes_Atlas);
SAMPLER(samplerURP_ReflProbes_Atlas);
float URP_ReflProbes_Count;

#ifndef SHADER_API_GLES3
CBUFFER_START(URP_ReflectionProbeBuffer)
#endif
    half4 URP_ReflProbes_HDR[MAX_REFLECTION_PROBES];
    float4 URP_ReflProbes_BoxMax[MAX_REFLECTION_PROBES];          // w contains the blend distance
    float4 URP_ReflProbes_BoxMin[MAX_REFLECTION_PROBES];          // w contains the importance
    float4 URP_ReflProbes_ProbePosition[MAX_REFLECTION_PROBES];   // w is positive for box projection, |w| is max mip level
    float4 URP_ReflProbes_MipScaleOffset[MAX_REFLECTION_PROBES * 7];
#ifndef SHADER_API_GLES3
CBUFFER_END
#endif

#if defined(USING_STEREO_MATRICES)
    #define USING_FP_CONSTANTS
#endif

#if defined(USING_FP_CONSTANTS)
    #define REQUIRES_VERTEX_CLUSTER_COORD_INTERPOLATOR
#endif

#if !defined(REQUIRES_VERTEX_CLUSTER_COORD_INTERPOLATOR)
    #define COMPUTE_CLUSTER_COORD_PIXEL
#endif

#if defined(USING_FP_CONSTANTS)
float3 _FPCameraPosWS;
float4x4 _FPViewMatrix;
float4x4 _FPViewProjMatrix;
#endif

// Select uint4 component by index.
// Helper to improve codegen for 2d indexing (data[x][y])
// Replace:
// data[i / 4][i % 4];
// with:
// select4(data[i / 4], i % 4);
uint ClusteringSelect4(uint4 v, uint i)
{
    // x = 0 = 00
    // y = 1 = 01
    // z = 2 = 10
    // w = 3 = 11
    //uint mask0 = uint(int(i << 31) >> 31);
    //uint mask1 = uint(int(i << 30) >> 31);
    //return
    //    (((v.w & mask0) | (v.z & ~mask0)) & mask1) |
    //    (((v.y & mask0) | (v.x & ~mask0)) & ~mask1);

    // Fix: compiler will generate wrong int_bitfieldExtract. 
    // int int_bitfieldExtract(int value, int offset, int bits) {
    //  return int((uint(value) >> uint(offset)) & ~(uint(0xffffffffu) << uint(bits)));
    // }
    return v[i];
}

#if SHADER_TARGET < 45
uint URP_FirstBitLow(uint m)
{
    // http://graphics.stanford.edu/~seander/bithacks.html#ZerosOnRightFloatCast
    return (asuint((float)(m & asuint(-asint(m)))) >> 23) - 0x7F;
}
#define FIRST_BIT_LOW URP_FirstBitLow
#else
#define FIRST_BIT_LOW firstbitlow
#endif

#define URP_TILE_SELECT4(tileIndex) ClusteringSelect4(asuint(URP_Tiles[tileIndex / 4]), tileIndex % 4)
#define URP_ZBIN_SELECT4(zBinIndex) ClusteringSelect4(asuint(URP_ZBins[zBinIndex / 4]), zBinIndex % 4)
#define URP_TILE_MASK(tileIndex, zBinIndex)  URP_TILE_SELECT4(tileIndex) & URP_ZBIN_SELECT4(zBinIndex)

float4 TransformWorldToClusterCoord(float3 positionWS)
{
#if defined(USING_FP_CONSTANTS)
    float4 posCS = mul(_FPViewProjMatrix, float4(positionWS, 1.0));
#else
    float4 posCS = mul(GetWorldToHClipMatrix(), float4(positionWS, 1.0));
#endif
    
    float4 ndc = posCS * 0.5f;
    ndc.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
    ndc.zw = posCS.zw;

    return ndc;
}

// internal
struct ClusterIterator
{
    uint tileOffset;
    uint zBinOffset;
    uint tileMask;
    // Stores the next light index in first 16 bits, and the max light index in the last 16 bits.
    uint entityIndexNextMax;
};

// internal
ClusterIterator ClusterInit(float2 normalizedScreenSpaceUV, float3 positionWS, int headerIndex)
{
    ClusterIterator state = (ClusterIterator)0;
    
#ifdef COMPUTE_CLUSTER_COORD_PIXEL 
    float4 clusterCoord = TransformWorldToClusterCoord(positionWS);
    normalizedScreenSpaceUV = clusterCoord.xy / clusterCoord.w;
#endif

    uint2 tileId = uint2(normalizedScreenSpaceUV * URP_FP_TILE_SCALE);
    state.tileOffset = (tileId.y * URP_FP_TILE_COUNT_X + tileId.x) * URP_FP_WORDS_PER_TILE;

#ifdef USING_FP_CONSTANTS
    float viewZ = dot(-_FPViewMatrix[2].xyz, positionWS - _FPCameraPosWS);
#else
    float viewZ = dot(GetViewForwardDir(), positionWS - GetCameraPositionWS());
#endif
    uint zBinBaseIndex = min(4*MAX_ZBIN_VEC4S - 1, (uint)(log2(viewZ) * URP_FP_ZBIN_SCALE + URP_FP_ZBIN_OFFSET)) * (2 + URP_FP_WORDS_PER_TILE);
    uint zBinHeaderIndex = zBinBaseIndex + headerIndex;
    state.zBinOffset = zBinBaseIndex + 2;

#if MAX_LIGHTS_PER_TILE > 32
    state.entityIndexNextMax = URP_ZBIN_SELECT4(zBinHeaderIndex);
#else
    uint tileIndex = state.tileOffset;
    uint zBinIndex = state.zBinOffset;
    if (URP_FP_WORDS_PER_TILE > 0)
    {
        state.tileMask = URP_TILE_MASK(tileIndex, zBinIndex);
    }
#endif

    return state;
}

// internal
bool ClusterNext(inout ClusterIterator it, out uint entityIndex)
{
#if MAX_LIGHTS_PER_TILE > 32
    uint maxIndex = it.entityIndexNextMax >> 16;
    while (it.tileMask == 0 && (it.entityIndexNextMax & 0xFFFF) <= maxIndex)
    {
        // Extract the lower 16 bits and shift by 5 to divide by 32.
        uint wordIndex = ((it.entityIndexNextMax & 0xFFFF) >> 5);
        uint tileIndex = it.tileOffset + wordIndex;
        uint zBinIndex = it.zBinOffset + wordIndex;
        it.tileMask = URP_TILE_MASK(tileIndex, zBinIndex) &
            // Mask out the beginning and end of the word.
            (0xFFFFFFFFu << (it.entityIndexNextMax & 0x1F)) & (0xFFFFFFFFu >> (31 - min(31, maxIndex - wordIndex * 32)));
        // The light index can start at a non-multiple of 32, but the following iterations should always be multiples of 32.
        // So we add 32 and mask out the lower bits.
        it.entityIndexNextMax = (it.entityIndexNextMax + 32) & ~31;
    }
#endif
    bool hasNext = it.tileMask != 0;
    uint bitIndex = FIRST_BIT_LOW(it.tileMask);
    it.tileMask ^= (1 << bitIndex);
#if MAX_LIGHTS_PER_TILE > 32
    // Subtract 32 because it stores the index of the _next_ word to fetch, but we want the current.
    // The upper 16 bits and bits representing values < 32 are masked out. The latter is due to the fact that it will be
    // included in what FIRST_BIT_LOW returns.
    entityIndex = (((it.entityIndexNextMax - 32) & (0xFFFF & ~31))) + bitIndex;
#else
    entityIndex = bitIndex;
#endif
    return hasNext;
}

#endif

#endif
