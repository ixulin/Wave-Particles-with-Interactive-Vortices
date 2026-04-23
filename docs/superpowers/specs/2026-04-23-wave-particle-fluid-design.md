# 2026-04-23 Wave Particle + Fluid 设计

## 目标

将当前"一次性随机生成、shader 内按初始状态硬算位置"的 wave particle 实现，重构为真正可更新的粒子系统，并与现有 fluid simulation 做单向耦合。

本轮目标只做：
1. wave particles 由 CPU 管理真实状态
2. fluid velocity advect wave particles
3. 保留现有 rtWaveParticle → PostProcess_H/V → WaterSurface 渲染链
4. 暂时关闭 wave particles → fluid velocity 的反向注入

不做：
- GPU compute 粒子系统
- 双向耦合闭环
- 新的 foam 模型
- 基于 density 的额外渲染

## 设计结论

采用 **方案 B：CPU 粒子池 + 固定容量动态 Mesh**。

原因：
- 能满足"底噪层 + 事件层"两层波粒子模型
- 可以真正维护粒子的当前位置、寿命和振幅，而不是每帧从初始状态重算
- 可以最小代价接入现有渲染管线
- 相比 `List` 动态增删，固定容量池更稳定，避免 GC 和频繁重分配

## 系统角色划分

### Fluid Simulation
负责生成二维平面流场：
- `rtVelocity`：局部流速
- `rtPressure`：不可压约束对应的压力场
- `rtDivergence`：投影前的发散量

fluid 的职责是告诉 wave particles：**当前位置的水在往哪里流**。

### Wave Particles
负责生成水面的高频高度细节。

wave particles 不替代 fluid，而是补足 fluid 无法直接表现的高频表面扰动。

最终效果应为：
- fluid 决定整体流向、绕障碍、尾涡
- wave particles 决定局部波纹、涟漪、细碎表面起伏

## 粒子分层模型

系统维护两类粒子，共用一个统一结构。

### 1. 底噪层粒子
用途：提供持续存在的高频表面细节。

特征：
- 初始化时一次性生成固定数量
- 长期存活，不使用寿命衰减
- 位置随机分布在整个水面上
- 方向随机
- 振幅较小，速度较低
- 到达边界后重新投放或 wrap

### 2. 事件层粒子
用途：在用户拖拽或后续其他事件发生时生成局部涟漪。

特征：
- Shift+LMB 拖拽时沿路径持续生成
- 每次生成一圈径向粒子
- 带寿命 `life/maxLife`
- 振幅和速度可与底噪层不同
- 死亡后回收到池中复用

## 数据结构

在 `WaveParticleSystem.cs` 中引入固定容量粒子池。

建议结构：

```csharp
struct WaveParticle
{
    public bool active;
    public byte layerType;   // 0 = ambient, 1 = event
    public Vector2 pos;
    public Vector2 dir;
    public float speed;
    public float amplitude;
    public float life;
    public float maxLife;
}
```

建议容量：
- 底噪层：2048~4096
- 事件层保留额外空位：2048 左右
- 总池大小先定为 4096 或 8192

固定容量池通过空闲索引或顺序扫描复用 slot，不在运行时增删容器。

## 推进模型

每帧更新所有 active 粒子：

```text
baseVel = dir * speed
fluidVel = sample(velocityCache, pos)
pos += (baseVel + fluidVel * fluidParticleStrength) * dt
```

### 底噪层
- 不死亡
- 可做轻微 wrap 或超界重投放
- amplitude 保持不变或只做极轻微随机抖动

### 事件层
- `life -= dt`
- `amplitude *= damping` 或按 `life / maxLife` 衰减
- `life <= 0` 时 `active = false`

## Flow → Particle 耦合方式

本轮使用 **异步缓存式 GPU→CPU 速度读取**。

### 方案
- 每隔若干帧对 `rtVelocity` 发起一次 `AsyncGPUReadback`
- 将读回数据存入 CPU 侧 `velocityCache`
- 粒子推进时从 `velocityCache` 双线性采样

### 原因
- 避免每帧同步 readback 卡住渲染线程
- 粒子对流场允许 1~3 帧延迟
- 对本项目而言，稳定的帧率比严格同步更重要

### 限制
- 刚注入的速度不会立刻影响粒子
- 粒子会有轻微滞后，但视觉通常可接受

## 事件生成模型

### Shift+LMB 拖拽
沿拖拽路径按固定距离采样生成中心点。

每个中心点生成一圈事件粒子：
- 均匀角度分布，例如 8 / 12 / 16 个方向
- `dir = (cosθ, sinθ)`
- 初始位置 = 生成中心
- 初始 speed / amplitude / life 使用独立参数

这样效果更接近石子入水或手划水产生的局部扩散波。

## 渲染路径改造

当前 `WaveParticle_Rasterize.shader` 负责：
- 从顶点里读初始状态
- 在 vertex shader 中自行推进粒子
- 输出 `(velocity, amplitude)`

重构后：

### CPU 负责
- 维护粒子当前状态
- 每帧将活粒子的 **当前 pos / dir / amplitude / speed** 写入动态 mesh

### Shader 负责
- 不再推进粒子
- 只将当前粒子属性写到 `rtWaveParticle`

即：`WaveParticle_Rasterize.shader` 退化为纯 rasterize/output shader。

## 动态 Mesh 方案

保留 `MeshTopology.Points`。

每帧：
1. 遍历活粒子
2. 填充顶点数组：
   - `vertex.xy = current pos`
   - `vertex.z = amplitude`
   - `uv = dir`
   - `normal.z = speed`
3. 用 `mesh.SetVertices/SetUVs/SetNormals/SetIndices` 更新当前活粒子数量
4. DrawMesh 到 `rtWaveParticle`

这样能最大程度复用现有 post-process 管线。

## 参数设计

新增建议参数：

### 底噪层
- `ambientParticleCount`
- `ambientParticleSpeedMin/Max`
- `ambientParticleAmplitudeMin/Max`

### 事件层
- `eventParticlesPerSpawn`
- `eventParticleLife`
- `eventParticleSpeed`
- `eventParticleAmplitude`
- `eventSpawnSpacing`
- `eventAmplitudeDamping`

### 耦合
- `fluidParticleStrength`
- `velocityReadbackInterval`

## 与当前系统的边界

### 保留不变
- `WaveParticle_PostProcess_H.shader`
- `WaveParticle_PostProcess_V.shader`
- `WaterSurface.shader` 对 `_DeviationTex` 的使用
- fluid simulation 的 pressure projection 主流程

### 本轮不启用
- `RunInjectWaveVelocity()`
- wave particle 反向注入 fluid

保留代码但默认关闭，避免调试时闭环自激。

## 测试与验收标准

### 验收 1：底噪层存在
- 不交互时 `_DeviationTex` 非全黑
- 水面存在稳定、连续的小尺度起伏
- 无明显整屏闪烁或周期性跳变

### 验收 2：事件层生效
- Shift+LMB 拖拽时，在路径附近生成明显局部涟漪
- 事件粒子会在若干秒内衰减消失
- 多次拖拽不会导致粒子无限增长

### 验收 3：fluid advect 生效
- 注入速度后，事件波纹会被流场带偏
- 障碍附近生成的波纹会绕障碍偏转，而非永远径向对称扩散

### 验收 4：性能可接受
- 常规交互下无明显 GC 抖动
- 粒子数达到设计上限时系统退化可控，不崩溃

## 风险与对策

### 风险 1：GPU readback 延迟导致粒子滞后
对策：
- 降低读取间隔
- 先接受 1~3 帧延迟作为第一版权衡

### 风险 2：动态 mesh 每帧完整重写开销较高
对策：
- 固定容量数组复用
- 只上传活粒子数量范围
- 如果仍有瓶颈，再升级到 Compute 版本

### 风险 3：底噪层太随机，看起来像噪点而不是波纹
对策：
- 调低随机性，限制方向分布
- 放宽 post-process blur 半径
- 允许底噪粒子使用分组方向而非完全随机

## 实施顺序

1. 将 `WaveParticle_Rasterize.shader` 改成纯输出 shader
2. 在 `WaveParticleSystem.cs` 中引入固定容量粒子池
3. 实现底噪层初始化与更新
4. 实现事件层 spawn / 衰减 / 回收
5. 增加 velocity cache 与 async readback
6. 将 fluid velocity 接入粒子推进
7. 调整参数到可接受视觉状态

## 明确不做的内容

- 本轮不重构为 Compute Shader 粒子系统
- 本轮不恢复 wave particle → fluid 双向注入
- 本轮不引入新的法线重建模型
- 本轮不改写现有 Wave PostProcess 数学
