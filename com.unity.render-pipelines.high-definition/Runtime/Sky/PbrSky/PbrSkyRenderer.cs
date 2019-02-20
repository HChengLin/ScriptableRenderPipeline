using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    public class PbrSkyRenderer : SkyRenderer
    {
        PbrSkySettings          m_Settings;
        // Store the hash of the parameters each time precomputation is done.
        // If the hash does not match, we must recompute our data.
        int                     lastPrecomputationParamHash;
        // Precomputed data below.
        RTHandleSystem.RTHandle m_OpticalDepthTable;
        RTHandleSystem.RTHandle m_GroundIrradianceTable;

        static ComputeShader    s_OpticalDepthTablePrecomputationCS;
        static ComputeShader    s_GroundIrradianceTablePrecomputationCS;

        [GenerateHLSL]
        public enum PbrSkyConfig
        {
            OpticalDepthTableSizeX    = 128,
            OpticalDepthTableSizeY    = 128,
            GroundIrradianceTableSize = 128,
        }

        public PbrSkyRenderer(PbrSkySettings settings)
        {
            m_Settings = settings;
        }

        public override bool IsValid()
        {
            /* TODO */
            return true;
        }

        public override void Build()
        {
            var hdrpAsset     = GraphicsSettings.renderPipelineAsset as HDRenderPipelineAsset;
            var hdrpResources = hdrpAsset.renderPipelineResources;

            // Shaders
            s_OpticalDepthTablePrecomputationCS     = hdrpResources.shaders.opticalDepthTablePrecomputationCS;
            s_GroundIrradianceTablePrecomputationCS = hdrpResources.shaders.groundIrradianceTablePrecomputationCS;


            Debug.Assert(s_OpticalDepthTablePrecomputationCS     != null);
            Debug.Assert(s_GroundIrradianceTablePrecomputationCS != null);

            // Textures
            m_OpticalDepthTable = RTHandles.Alloc((int)PbrSkyConfig.OpticalDepthTableSizeX, (int)PbrSkyConfig.OpticalDepthTableSizeY,
                                                  filterMode: FilterMode.Bilinear, colorFormat: GraphicsFormat.R16G16_SFloat,
                                                  enableRandomWrite: true, xrInstancing: false, useDynamicScale: false,
                                                  name: "OpticalDepthTable");


            m_GroundIrradianceTable = RTHandles.Alloc((int)PbrSkyConfig.GroundIrradianceTableSize, 1,
                                                  filterMode: FilterMode.Bilinear, colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
                                                  enableRandomWrite: true, xrInstancing: false, useDynamicScale: false,
                                                  name: "GroundIrradianceTable");

            Debug.Assert(m_OpticalDepthTable     != null);
            Debug.Assert(m_GroundIrradianceTable != null);
        }

        public override void Cleanup()
        {
            /* TODO */
        }

        public override void SetRenderTargets(BuiltinSkyParameters builtinParams)
        {
            /* TODO: why is this overridable? */

            if (builtinParams.depthBuffer == BuiltinSkyParameters.nullRT)
            {
                HDUtils.SetRenderTarget(builtinParams.commandBuffer, builtinParams.hdCamera, builtinParams.colorBuffer);
            }
            else
            {
                HDUtils.SetRenderTarget(builtinParams.commandBuffer, builtinParams.hdCamera, builtinParams.colorBuffer, builtinParams.depthBuffer);
            }
        }

        void UpdateSharedConstantBuffer(CommandBuffer cmd)
        {
            cmd.SetGlobalFloat( "_PlanetaryRadius",           m_Settings.planetaryRadius);
            cmd.SetGlobalFloat( "_AtmosphericLayerHeight",    m_Settings.atmosphericLayerHeight);
            cmd.SetGlobalFloat( "_AirDensityFalloff",         m_Settings.airDensityFalloff);
            cmd.SetGlobalFloat( "_AirScaleHeight",            1.0f / m_Settings.airDensityFalloff);
            cmd.SetGlobalFloat( "_AerosolDensityFalloff",     m_Settings.aerosolDensityFalloff);
            cmd.SetGlobalFloat( "_AerosolScaleHeight",        1.0f / m_Settings.airDensityFalloff);
            cmd.SetGlobalVector("_SunRadiance",               m_Settings.sunRadiance.value);
            cmd.SetGlobalFloat( "_RcpAtmosphericLayerHeight", 1.0f / m_Settings.atmosphericLayerHeight);
            cmd.SetGlobalVector("_AirSeaLevelExtinction",     m_Settings.airThickness.value     * 0.001f); // Convert to 1/km
            cmd.SetGlobalFloat( "_AerosolSeaLevelExtinction", m_Settings.aerosolThickness.value * 0.001f); // Convert to 1/km
        }

        void PrecomputeTables(CommandBuffer cmd)
        {
            using (new ProfilingSample(cmd, "Optical Depth Table Precomputation"))
            {
                cmd.SetComputeTextureParam(s_OpticalDepthTablePrecomputationCS, 0, "_OpticalDepthTable", m_OpticalDepthTable);
                cmd.DispatchCompute(s_OpticalDepthTablePrecomputationCS, 0, (int)PbrSkyConfig.OpticalDepthTableSizeX / 8, (int)PbrSkyConfig.OpticalDepthTableSizeY / 8, 1);
            }

            using (new ProfilingSample(cmd, "Ground Irradiance Table Precomputation"))
            {
                cmd.SetComputeTextureParam(s_GroundIrradianceTablePrecomputationCS, 0, "_OpticalDepthTexture",   m_OpticalDepthTable);
                cmd.SetComputeTextureParam(s_GroundIrradianceTablePrecomputationCS, 0, "_GroundIrradianceTable", m_GroundIrradianceTable);
                cmd.DispatchCompute(s_GroundIrradianceTablePrecomputationCS, 0, (int)PbrSkyConfig.GroundIrradianceTableSize / 64, 1, 1);
            }
        }

        // 'renderSunDisk' parameter is meaningless and is thus ignored.
        public override void RenderSky(BuiltinSkyParameters builtinParams, bool renderForCubemap, bool renderSunDisk)
        {
            CommandBuffer cmd = builtinParams.commandBuffer;

            m_Settings.UpdateParameters(builtinParams);
            UpdateSharedConstantBuffer(cmd);

            int currentParamHash = m_Settings.GetHashCode();

            if (currentParamHash != lastPrecomputationParamHash)
            {
                PrecomputeTables(cmd);

                //lastPrecomputationParamHash = currentParamHash;
            }

            /* TODO */
        }
    }
}
