/*           INFINITY CODE          */
/*     https://infinity-code.com    */

namespace InfinityCode.MeshToTerrain
{
    public enum MeshToTerrainBounds
    {
        autoDetect,
        fromGameobject,
        selectBounds
    }

    public enum MeshToTerrainDirection
    {
        normal,
        reversed
    }

    public enum MeshToTerrainFindType
    {
        gameObjects,
        layers
    }

    public enum MeshToTerrainHoles
    {
        minimumValue,
        neighborAverage,
        remove
    }

    public enum MeshToTerrainPhase
    {
        idle,
        prepare,
        createTerrains,
        generateHeightmaps,
        generateTextures,
        finish,
        prepareTextures
    }

    public enum MeshToTerrainSelectTerrainType
    {
        existTerrains,
        newTerrains
    }

    public enum MeshToTerrainTextureCaptureMode
    {
        camera,
        raycast
    }

    public enum MeshToTerrainTextureResultType
    {
        regularTexture,
        hugeTexture
    }

    public enum MeshToTerrainYRange
    {
        minimalRange,
        longMeshSide,
        fixedValue
    }
}