# TotkCave Class Library

`TotkCave` is a high-performance **.NET 10** class library for parsing, decompressing, decoding, building 3D meshes, and exporting Wavefront `.obj`/`.mtl` files for *Tears of the Kingdom* cave (`cave017`) and Depths terrain (`.quad`) streaming files.

---

## Features

- **Binary Parsers**: Memory-mapped binary parsing of `C.crbin` index files (`CrBin`, `QuadResource`).
- **Zero-Allocation Vertex Decoding**: High-speed bitwise extraction of 28-byte vertex strides, cell-relative quantized positions, octahedral normals, baked AO colors, and material splat weights (`VertexDecoder`).
- **Flexible Page Management**: Multi-tier page provider supporting console-dumped decompressed pages, stamped disk caches, and automatic MeshCodec CLI process execution (`CavePageSource`, `QuadPageSource`).
- **World-Space Mesh Builders**: Reconstructs complete 3D surface meshes at any specified LOD level with vertex welding and edge-length threshold cleaning filters (`MeshBuilder`, `QuadMeshBuilder`).
- **Wavefront OBJ/MTL Exporter**: Exports `.obj` and `.mtl` files with computed triplanar UV projection coordinates (`ObjExporter`).

---

## Installation & Usage

### 1. Build Library
```bash
dotnet build TotkCave.sln -c Release
```

### 2. Reference in Your C# Project
```xml
<ItemGroup>
  <ProjectReference Include="path/to/TotkCave/TotkCave.csproj" />
</ItemGroup>
```

### 3. Example Code
```csharp
using TotkCave.Models;
using TotkCave.PageSource;
using TotkCave.Building;
using TotkCave.Exporting;

// Parse CRBIN container file
CrBin cr = CrBin.FromFile("Cave_Akkala_0000/C.crbin");

// Initialize Page Source (handles console dumps & auto-invokes MeshCodec CLI for RomFS chunks)
CavePageSource pages = new(cr, caveDir: "Cave_Akkala_0000");

// Reconstruct world-space 3D surface mesh at finest LOD
CaveMesh mesh = MeshBuilder.BuildMesh(cr, pages, lod: cr.NumSubdivisions, weld: true);

// Export to OBJ + MTL with triplanar UV projections
ObjExporter.WriteObj(mesh, "Cave_Akkala_0000.obj", new ObjExportOptions(
    IncludeColors: true,
    IncludeNormals: true,
    IncludeGroups: true,
    IncludeMaterials: true
));
```

---

## Structure

```
TotkCave/
├── TotkCave.csproj
├── TotkCave.sln
├── .gitignore
├── README.md
├── Models/
│   ├── CrBin.cs                  Binary parser for surface cave C.crbin containers
│   ├── CrBinModels.cs            CrBinNode, CrBinStream, CrBinMaterial, CrBinPageFile
│   ├── ChunkHeader.cs            ResChunkHeader reader for compressed chunk headers
│   ├── CaveMesh.cs               Reconstructed 3D geometry container
│   └── QuadResource.cs           Binary parser for Depths quad containers (section 0xE4)
├── Decoding/
│   ├── VertexDecoder.cs          28-byte vertex stride & 96-bit attribute block decoder
│   └── OctNormalDecoder.cs       7-bit octahedral normal decoder
├── PageSource/
│   ├── IPageSource.cs            Interface for page retrieval
│   ├── PageSource.cs             Console dump, cache, & MeshCodec CLI provider
│   └── QuadPageSource.cs         Depths .quad page reader & Zstd fallback
├── Building/
│   ├── MeshBuilder.cs            Surface cave 3D mesh generator
│   └── QuadMeshBuilder.cs        Depths quad terrain 3D mesh generator
├── Exporting/
│   └── ObjExporter.cs            OBJ / MTL exporter with triplanar UV projections
├── Validation/
│   └── MeshValidator.cs          Heuristic sanity checks for mesh integrity
└── Utils/
    └── CaveFinder.cs             Directory scanner for C.crbin files
```
