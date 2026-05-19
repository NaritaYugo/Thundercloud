using UnityEngine;
using System.Reflection;
using UnityEngine.Rendering;

[RequireComponent(typeof(Renderer))]
public class Manager : MonoBehaviour
{
    public class KernelSet
    {
        public int CreateCloud, ScrollCloud, UpdateBolt, SolvePoisson, EvokeBolt, 
            ClearTextures, ClearBuffers, CopyLight, PropagateFlash1, PropagateFlash4;
        
        public void Initialize(ComputeShader cs)
        {
            FieldInfo[] fields = GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(int))
                {
                    int id = cs.FindKernel(field.Name);
                    field.SetValue(this, id);
                }
            }
        }
    }

    public class TextureSet
    {
        public RenderTexture cloud, scrollCloud, potentialA, potentialB, flashRef, flash1A, flash1B, flash4A, flash4B, flashInt;

        public void ReleaseAll()
        {
            FieldInfo[] fields = GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.GetValue(this) is RenderTexture rt)
                {
                    rt.Release();
                    Destroy(rt);
                }
            }
        }
    }

    public class BufferSet
    {
        public ComputeBuffer allSegments, segmentCount, isAliveTip, tipA, tipB, args;

        public void ReleaseAll()
        {
            FieldInfo[] fields = this.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.GetValue(this) is ComputeBuffer cb)
                {
                    cb.Release();
                    cb.Dispose();
                }
            }
        }
    }

    public struct Tip
    {
        public Vector3 currPos;
        public Vector3 prevPos; // 折れ線でつなぐため
    }

    [Header("Unity Settings")]
    [SerializeField] private ComputeShader compute;
    [SerializeField] private Material boltMaterial;

    [Header("Simulation Size")]
    [SerializeField] private Vector3Int m_GridRes = new(64, 64, 64);
    [SerializeField] private int m_MaxBranches = 100;
    [SerializeField] private int m_MaxTips = 1000;
    [SerializeField] private float m_dt = 0.001f;

    [Header("Iterations")]
    [SerializeField] private int m_BoltExtendIter = 100;
    [SerializeField] private int m_PoissonIter = 20;
    [SerializeField] private int m_CoarseBlurIter = 30;
    [SerializeField] private int m_FineBlurIter = 30;

    [Header("Behavior")]
    [SerializeField] private float m_EvokeProbability = 0.1f;
    [SerializeField] private float m_BranchProbability = 0.2f;
    [SerializeField] private float m_ScrollSpeed = 0.1f;
    [SerializeField] private float m_Absorption = 0.8f;
    [SerializeField] private float m_BranchScale = 10f;
    [SerializeField] private float m_FlashFadeFactor = 0.3f;

    private Material cloudMaterial;

    private readonly KernelSet kernels = new();
    private readonly TextureSet textures = new();
    private readonly BufferSet buffers = new();

    void Start()
    {
        kernels.Initialize(compute);

        InitializeRenderTextures();
        InitializeComputeBuffers();

        cloudMaterial = GetComponent<Renderer>().material;
        cloudMaterial.SetTexture("_MainTex", textures.cloud);
        cloudMaterial.SetTexture("_FlashTex", textures.flash1A);
        cloudMaterial.SetFloat("_ScrollSpeed", m_ScrollSpeed);

        BakeCloud();
    }

    void InitializeRenderTextures()
    {
        static RenderTexture CreateRT(Vector3Int resolution, RenderTextureFormat format)
        {
            RenderTexture rt = new(resolution.x, resolution.y, 0, format)
            {
                dimension = TextureDimension.Tex3D,
                volumeDepth = resolution.z,
                enableRandomWrite = true,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            rt.Create();
            return rt;
        }

        textures.cloud = CreateRT(m_GridRes, RenderTextureFormat.RFloat);
        textures.scrollCloud = CreateRT(m_GridRes, RenderTextureFormat.RFloat);
        textures.flashRef = CreateRT(m_GridRes, RenderTextureFormat.RFloat);
        textures.flash1A = CreateRT(m_GridRes, RenderTextureFormat.RFloat);
        textures.flash1B = CreateRT(m_GridRes, RenderTextureFormat.RFloat);
        textures.flash4A = CreateRT(m_GridRes/4, RenderTextureFormat.RFloat);
        textures.flash4B = CreateRT(m_GridRes/4, RenderTextureFormat.RFloat);
        textures.flashInt = CreateRT(m_GridRes, RenderTextureFormat.RInt);
        textures.potentialA = CreateRT(m_GridRes, RenderTextureFormat.RFloat);
        textures.potentialB = CreateRT(m_GridRes, RenderTextureFormat.RFloat);
    }
    
    void InitializeComputeBuffers()
    {
        buffers.allSegments = new ComputeBuffer(m_MaxBranches, 24);
        
        buffers.segmentCount = new ComputeBuffer(1, sizeof(int));
        buffers.segmentCount.SetData(new int[] { 0 });

        buffers.isAliveTip = new ComputeBuffer(1, sizeof(int));
        buffers.isAliveTip.SetData(new int[] { 0 });

        buffers.tipA = new ComputeBuffer(m_MaxTips, 24, ComputeBufferType.Append);
        buffers.tipB = new ComputeBuffer(m_MaxTips, 24, ComputeBufferType.Append);
        
        buffers.args = new ComputeBuffer(3, sizeof(int), ComputeBufferType.IndirectArguments);
        buffers.args.SetData(new int[] { 0, 1, 1 });
    }

    void BakeCloud()
    {
        compute.SetInts("_GridRes", m_GridRes.x, m_GridRes.y, m_GridRes.z);

        compute.SetTexture(kernels.CreateCloud, "_Cloud", textures.cloud);
        Dispatch(compute, kernels.CreateCloud, m_GridRes);
    }

    void Update()
    {
        compute.SetFloat("dt", m_dt);
        compute.SetFloat("t", Time.time);
        compute.SetFloat("_EvokeProbability", m_EvokeProbability);
        compute.SetFloat("_BranchProbability", m_BranchProbability);
        compute.SetFloat("_Absorption", m_Absorption);
        compute.SetFloat("_BranchScale", m_BranchScale);
        compute.SetFloat("_ScrollSpeed", m_ScrollSpeed);
        compute.SetFloat("_FlashFadeFactor", m_FlashFadeFactor);
        compute.SetInt("_MaxBranches", m_MaxBranches);

        compute.SetTexture(kernels.ScrollCloud, "_Cloud", textures.cloud);
        compute.SetTexture(kernels.ScrollCloud, "_ScrollCloud", textures.scrollCloud);
        compute.SetTexture(kernels.ScrollCloud, "W_Potential", textures.potentialA);
        Dispatch(compute, kernels.ScrollCloud, m_GridRes);

        compute.SetTexture(kernels.ClearTextures, "_FlashInt", textures.flashInt);
        compute.SetTexture(kernels.ClearTextures, "R_Flash1", textures.flash1A);
        compute.SetTexture(kernels.ClearTextures, "W_Flash1", textures.flash1B);
        compute.SetTexture(kernels.ClearTextures, "R_Flash4", textures.flash4A);
        compute.SetTexture(kernels.ClearTextures, "W_Flash4", textures.flash4B);
        compute.SetTexture(kernels.ClearTextures, "_FlashRef", textures.flashRef);
        compute.SetBuffer(kernels.ClearTextures, "_IsAliveTip", buffers.isAliveTip);
        Dispatch(compute, kernels.ClearTextures, m_GridRes);
        (textures.flash1A, textures.flash1B) = (textures.flash1B, textures.flash1A);
        (textures.flash4A, textures.flash4B) = (textures.flash4B, textures.flash4A);
        
        compute.SetBuffer(kernels.ClearBuffers, "_AllSegments", buffers.allSegments);
        compute.SetBuffer(kernels.ClearBuffers, "_IsAliveTip", buffers.isAliveTip);
        Dispatch(compute, kernels.ClearBuffers, new Vector3Int(m_MaxBranches, 1, 1));

        for (int i = 0; i < m_PoissonIter; i++) {
            compute.SetTexture(kernels.SolvePoisson, "R_Potential", textures.potentialA);
            compute.SetTexture(kernels.SolvePoisson, "W_Potential", textures.potentialB);
            compute.SetTexture(kernels.SolvePoisson, "_ScrollCloud", textures.scrollCloud);
            Dispatch(compute, kernels.SolvePoisson, m_GridRes);
            (textures.potentialA, textures.potentialB) = (textures.potentialB, textures.potentialA);
        }

        buffers.tipA.SetCounterValue(0);
        compute.SetTexture(kernels.EvokeBolt, "R_Potential", textures.potentialA);
        compute.SetBuffer(kernels.EvokeBolt, "_NextTips", buffers.tipA);
        compute.SetBuffer(kernels.EvokeBolt, "_AllSegments", buffers.allSegments);
        compute.SetBuffer(kernels.EvokeBolt, "_SegmentCount", buffers.segmentCount);
        compute.SetBuffer(kernels.EvokeBolt, "_IsAliveTip", buffers.isAliveTip);
        Dispatch(compute, kernels.EvokeBolt, new Vector3Int(1, 1, 1));
        
        for (int s = 0; s < m_BoltExtendIter; s++) 
        {
            ComputeBuffer.CopyCount(buffers.tipA, buffers.args, 0);

            buffers.tipB.SetCounterValue(0);

            compute.SetTexture(kernels.UpdateBolt, "R_Potential", textures.potentialA);
            compute.SetTexture(kernels.UpdateBolt, "_FlashInt", textures.flashInt);
            compute.SetBuffer(kernels.UpdateBolt, "_CurrentTips", buffers.tipA);
            compute.SetBuffer(kernels.UpdateBolt, "_NextTips", buffers.tipB);
            compute.SetBuffer(kernels.UpdateBolt, "_AllSegments", buffers.allSegments);
            compute.SetBuffer(kernels.UpdateBolt, "_SegmentCount", buffers.segmentCount);
            compute.SetBuffer(kernels.UpdateBolt, "_IsAliveTip", buffers.isAliveTip);
            compute.DispatchIndirect(kernels.UpdateBolt, buffers.args);

            (buffers.tipA, buffers.tipB) = (buffers.tipB, buffers.tipA);
            buffers.tipB.SetCounterValue(0);
        }

        compute.SetTexture(kernels.CopyLight, "_FlashInt", textures.flashInt);
        compute.SetTexture(kernels.CopyLight, "W_Flash1", textures.flash1A);
        compute.SetTexture(kernels.CopyLight, "W_Flash4", textures.flash4A);
        compute.SetTexture(kernels.CopyLight, "_FlashRef", textures.flashRef);
        Dispatch(compute, kernels.CopyLight, m_GridRes);

        // 低解像度
        for (int i = 0; i < m_CoarseBlurIter; i++) {
            compute.SetTexture(kernels.PropagateFlash4, "R_Flash4", textures.flash4A);
            compute.SetTexture(kernels.PropagateFlash4, "W_Flash4", textures.flash4B);
            compute.SetTexture(kernels.PropagateFlash4, "_ScrollCloud", textures.scrollCloud);
            Dispatch(compute, kernels.PropagateFlash4, m_GridRes/4);
            (textures.flash4A, textures.flash4B) = (textures.flash4B, textures.flash4A);
        }

        // 高解像度
        for (int i = 0; i < m_FineBlurIter; i++) {
            compute.SetTexture(kernels.PropagateFlash1, "R_Flash4", textures.flash4A);
            compute.SetTexture(kernels.PropagateFlash1, "R_Flash1", textures.flash1A);
            compute.SetTexture(kernels.PropagateFlash1, "W_Flash1", textures.flash1B);
            compute.SetTexture(kernels.PropagateFlash1, "_FlashRef", textures.flashRef);
            compute.SetTexture(kernels.PropagateFlash1, "_ScrollCloud", textures.scrollCloud);
            Dispatch(compute, kernels.PropagateFlash1, m_GridRes);
            (textures.flash1A, textures.flash1B) = (textures.flash1B, textures.flash1A);
        }
    }

    void Dispatch(ComputeShader _compute, int kernelID, Vector3Int size)
    {
        _compute.GetKernelThreadGroupSizes(kernelID, out uint x, out uint y, out uint z);
        _compute.Dispatch(kernelID, (int)((size.x + x - 1)/x), (int)((size.y + y - 1)/y), (int)((size.z + z - 1)/z));
    }

    // BoltはDrawProceduralなので、URPの予約描画処理が必要
    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (boltMaterial == null || buffers.allSegments == null) return;

        boltMaterial.SetBuffer("_BoltBuffer", buffers.allSegments);
        boltMaterial.SetVector("_GridRes", (Vector3)m_GridRes);
        boltMaterial.SetFloat("_ScrollSpeed", m_ScrollSpeed);
        boltMaterial.SetVector("_BoundsSize", transform.localScale);
        boltMaterial.SetVector("_Locate", transform.localPosition);

        boltMaterial.SetTexture("_MainTex", textures.cloud);
        boltMaterial.SetBuffer("_IsAliveTip", buffers.isAliveTip);

        // メッシュの描画ではないので、カメラ座標をC#から一括で渡したほうが高速
        Vector3 camPosOS = transform.InverseTransformPoint(camera.transform.position);
        Vector3 camUV = new(
            camPosOS.x / transform.localScale.x + 0.5f,
            camPosOS.y / transform.localScale.y + 0.5f,
            camPosOS.z / transform.localScale.z + 0.5f
        );
        boltMaterial.SetVector("_CameraUV", camUV);

        boltMaterial.SetPass(0);
        Graphics.DrawProcedural(boltMaterial, new Bounds(transform.position, transform.lossyScale), 
            MeshTopology.Lines, 2, m_MaxBranches, camera);
    }

    void OnDestroy()
    {
        textures.ReleaseAll();
        buffers.ReleaseAll();
    }
}
