# Generated 3D Models

This directory stores 3D character models generated via Tripo3D.

## Workflow

1. Open **Tripo Studio** (studio.tripo3d.ai) + **Unity** (Tools → Tripo Bridge)
2. Upload a Cut Out portrait from `C:\Pictures\Assets\D&d UI assets\NPCs\Cut out\`
3. Settings: HD Model, Smart Mesh, Ultra quality, Texture ON
4. Click **Generate Model** (costs ~50 credits per model)
5. Review the 3D model in Tripo's viewer — rotate, check quality
6. Click **Export** → Unity sends it via DCC Bridge
7. Model lands here in `Assets/GeneratedModels/Characters/`
8. Rename to a semantic name: `garth.glb`, `thalia.glb`, etc.

## Budget

- **Free tier:** ~350 models/month (100 free credits, ~50 credits per HD model = varies)
- **Strategy:** Generate key NPCs first, then enemies, build library slowly
- **Every model is cached permanently** — never re-generate what you already have

## File naming convention

`<character_name>.glb` — lowercase, underscores for spaces
Examples: `garth.glb`, `thalia.glb`, `wolf_alpha.glb`, `skeleton_archer.glb`

## Characters folder

| Subfolder | Contents |
|---|---|
| `Characters/` | Player characters, NPCs, enemies |
| `Props/` (future) | Weapons, armor, items |
| `Creatures/` (future) | Non-humanoid monsters |
