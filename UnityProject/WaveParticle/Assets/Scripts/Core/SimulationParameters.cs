using UnityEngine;

[CreateAssetMenu(menuName = "Water/SimulationParameters")]
public class SimulationParameters : ScriptableObject
{
    [Header("Wave Particles")]
    public float heightScale = 0.14f;
    public float waveParticleSpeedScale = 0.00005f;
    public float flowSpeed = 0.000931f;
    public float dxScale = 0.03f;
    public float dzScale = 0.03f;
    public float timeScale = 1.3f;
    public float foamScale = 5.0f;
    public int blurRadius = 15;
    public int ambientParticleCount = 2048;
    public float ambientParticleSpeedMin = 0.05f;
    public float ambientParticleSpeedMax = 0.2f;
    public float ambientParticleAmplitudeMin = 0.03f;
    public float ambientParticleAmplitudeMax = 0.08f;
    public int eventParticlesPerSpawn = 12;
    public float eventParticleSpeed = 0.6f;
    public float eventParticleAmplitude = 0.2f;
    public float eventParticleLife = 1.25f;
    public float eventSpawnSpacing = 0.08f;
    public float eventAmplitudeDamping = 0.96f;
    public int velocityReadbackInterval = 2;

    [Header("Fluid Simulation")]
    public float timeStepFluid = 0.03f;
    public float fluidCellSize = 0.6f;
    public float fluidDissipation = 0.994f;
    public float vorticityScale = 0.64f;
    public float splatDirU = 1.0f;
    public float splatDirV = 0.0f;
    public float splatScale = 0.00593f;
    public float splatDensityU = 0.5f;
    public float splatDensityV = 0.5f;
    public float splatDensityRadius = 0.1f;
    public float splatDensityScale = 0.01f;
    public int jacobiIterations = 40;
    public int fluidSimulationInterval = 30;
    public float velocityImpulseRadius = 0.08f;
    public float velocityImpulseStrength = 1.5f;

    [Header("Obstacle")]
    public float brushScale = 0.1f;
    public float brushStrength = 1.0f;
    public float obstacleScale = 1.8f;
    public float obstacleThresholdFluid = 0.3f;
    public float obstacleThresholdWave = 0.12f;

    [Header("Tessellation")]
    public int edgeTessFactor = 7;
    public int insideTessFactor = 5;
    public int waterCellCount = 50;

    [Header("Texture Dimensions")]
    public int textureWidth = 500;
    public int textureHeight = 500;
    public int textureWidthFluid = 142;
    public int textureHeightFluid = 142;

    [Header("Shading")]
    public float lightHeight = 9.35f;
    public float extinctionCoeff = -0.41f;
    public float shininess = 340f;
    public float fresnelBias = 0.0f;
    public float fresnelPow = 3.0f;
    public float fresnelScale = 0.68f;
    public float foamPow = 9.6f;
    public int renderMode = 11;
    public float fluidHeightScale = 0.5f;
    public float fluidParticleStrength = 0.02f;
    public float waveInjectStrength = 200f;
}
