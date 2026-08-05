# Audio resources

`AudioManager` automatically loads optional audio assets from this folder by name:

- `MenuMusic`
- `GameplayMusic`
- `ButtonHover`
- `ButtonClick`
- `EnemyHit`
- `PlayerHurt`
- `Jump`
- `Land`
- `BossWarning`
- `BossSpawn`

An optional mixer named `GameAudioMixer` can provide `Music` and `SFX` groups.
If an `AudioManager` prefab is added here, the runtime bootstrap will instantiate it;
otherwise the manager and its three audio sources are created automatically.
