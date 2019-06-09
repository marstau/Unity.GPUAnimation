using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace GPUAnimPackage
{
    public class InstancedSkinningDrawer : IDisposable
    {
        private const int PreallocatedBufferSize = 32 * 1024;

        private ComputeBuffer argsBuffer;

        private readonly uint[] indirectArgs = new uint[5] { 0, 0, 0, 0, 0 };

        private ComputeBuffer textureCoordinatesBuffer;
        private ComputeBuffer objectToWorldBuffer;

        public NativeList<float3> TextureCoordinates;
        public NativeList<float4x4> ObjectToWorld;

        private Material material;

        private Mesh mesh;
        
        public unsafe InstancedSkinningDrawer(Material srcMaterial, Mesh meshToDraw, AnimationTextures animTexture)
        {
            this.mesh = meshToDraw;
            this.material = new Material(srcMaterial);

            argsBuffer = new ComputeBuffer(1, indirectArgs.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            indirectArgs[0] = mesh.GetIndexCount(0);
            indirectArgs[1] = (uint)0;
            argsBuffer.SetData(indirectArgs);

            objectToWorldBuffer = new ComputeBuffer(PreallocatedBufferSize, 16 * sizeof(float));
            textureCoordinatesBuffer = new ComputeBuffer(PreallocatedBufferSize, 3 * sizeof(float));

            ObjectToWorld = new NativeList<float4x4>(PreallocatedBufferSize, Allocator.Persistent);
            TextureCoordinates = new NativeList<float3>(PreallocatedBufferSize, Allocator.Persistent);
		
            this.material.SetBuffer("textureCoordinatesBuffer", textureCoordinatesBuffer);
            this.material.SetBuffer("objectToWorldBuffer", objectToWorldBuffer);
            this.material.SetTexture("_AnimationTexture0", animTexture.Animation0);
            this.material.SetTexture("_AnimationTexture1", animTexture.Animation1);
            this.material.SetTexture("_AnimationTexture2", animTexture.Animation2);
        }

        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(material);
		
            if (argsBuffer != null) argsBuffer.Dispose();

            if (objectToWorldBuffer != null) objectToWorldBuffer.Dispose();
            if (ObjectToWorld.IsCreated) ObjectToWorld.Dispose();

            if (textureCoordinatesBuffer != null) textureCoordinatesBuffer.Dispose();
            if (TextureCoordinates.IsCreated) TextureCoordinates.Dispose();
        }

        public void Draw()
        {
            if (objectToWorldBuffer == null)
                return;
            // CHECK: Systems seem to be called when exiting playmode once things start getting destroyed, such as the mesh here.
            if (mesh == null || material == null) 
                return;

            int count = UnitToDrawCount;
            if (count == 0) return;

            Profiler.BeginSample("Modify compute buffers");

            Profiler.BeginSample("Shader set data");

            objectToWorldBuffer.SetData(ObjectToWorld.AsArray(), 0, 0, count);
            textureCoordinatesBuffer.SetData(TextureCoordinates.AsArray(), 0, 0, count);
            
            Profiler.EndSample();

            Profiler.EndSample();

            //indirectArgs[1] = (uint)data.Count;
            indirectArgs[1] = (uint)count;
            argsBuffer.SetData(indirectArgs);

            Graphics.DrawMeshInstancedIndirect(mesh, 0, material, new Bounds(Vector3.zero, 1000000 * Vector3.one), argsBuffer, 0, new MaterialPropertyBlock(), ShadowCastingMode.Off, true);
        }

        public int UnitToDrawCount
        {
            get
            {
                return ObjectToWorld.Length;
            }
        }
    }
}