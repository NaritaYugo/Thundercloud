# ボリュメトリック雷雲

GPUによる雲の描画と、雲内放電のシミュレーションです。

## デモ

[![Demo](https://github.com/user-attachments/assets/c757b606-7022-4cc0-b23a-e06d2b983620)](https://youtu.be/xiqlfRx6dBA)

[Windows Demo ダウンロード](https://github.com/NaritaYugo/Thundercloud/releases/tag/v1.0.0)

※ Unityでビルドした未署名の実行ファイルのため、
Windows Defender の警告が表示される場合があります。

※高頻度でリアルに発光させると目に悪いため、青系に寄せて発光を抑えています。

## 概要

雲の中で起こる放電現象を Compute Shader でシミュレーションしました。
雷の発生から電位に従った伸長まで、CPUからシグナルを送るのではなく、GPUで完結させています。

また、雲はタイリング可能なテクスチャをスタート時にベイクし、スクロールすることで、計算量をレイマーチングに割いています。


## システム構成
### 雲テクスチャ
ボロノイノイズ、パーリンノイズを組み合わせた fBm (フラクタル)ノイズ

### 雲のレンダリング
レイマーチングの各点から光源方向にサブサイクルを回すシャドウマーチング

### 雷の伸長処理
DBM (誘電破壊モデル: Dielectric Breakdown Model) をベースに、先頭からのみ枝分かれするようにして軽量化

### 雲内の光拡散
解像度の違うグリッドを用いてイテレーション回数を減らした、マルチグリッドのガウシアンブラー



## ディレクトリ構成
ファイル数が少ないのでMainにまとめています。

```text
Assets
├─ ForDemo
│  └─ OrbitCamera.cs
├─ Main
│  ├─ BoltRender.shader
│  ├─ CloudRender.shader
│  ├─ Custom_Bolt.mat
│  ├─ Custom_Cloud.mat
│  ├─ Manager.cs
│  └─ ThunderSim.compute
├─ Scenes
│  └─ MainScene.unity
└─ Settings
```

## 実行方法
1. 本リポジトリをクローンするか、ZIPでダウンロードします。
2. Unity Hubから対象のバージョンでプロジェクトを開きます。
3. Scenes/MainScene を開いてプレイモードを実行してください。
4. WASDで注視回転することができます。
5. Directinal Light 内の色温度を調整することで、雲に当たる光の色を調整することができます。

## ライセンス

MIT
