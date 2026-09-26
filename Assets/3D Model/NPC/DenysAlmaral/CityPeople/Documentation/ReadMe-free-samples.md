![](images/IsoCity_screenShot_820w-free.png)
# City People FREE Samples

Welcome to the **City People FREE Samples**! This package provides a group of city characters to bring life to your Unity projects. This is a subset of [**City People Mega-Pack**](https://assetstore.unity.com/packages/3d/characters/city-people-mega-pack-203329) which contains 120+ of diverse characters.

## Table of Contents

1. [Introduction](#introduction)
2. [What's New in v1.4.0](#whats-new-in-v130)
3. [Getting Started](#getting-started)
4. [Demo Scenes](#demo-scenes)
   - [Demo Scene 1: Character Showcase](#demo-scene-1-character-showcase)
   - [Demo Scene 2: Isometric City](#demo-scene-2-isometric-city)
5. [Animations](#animations)
6. [CityPeople Component Script](#citypeople-component-script)
7. [Palette System and UV](#palette-system-and-uv)
8. [Support](#support)

---

## Introduction

These characters packs are designed to populate your urban environments with a rich variety of animated characters. The Polyart style provides optimized characters, making them suitable for low-end devices and AR/VR simulations.

## What's New in v1.4.0

- **Added two more characters!!:** Construction guy and girl with prosthetic leg.
- Added construction tools and specific animations and demonstrative script.
- Included material converter patch for both ways URP and Built-in
- A Christmas hat!

## Quick Start

1. **Import the Package**: Download and import into your Unity project.
2. **Explore Demo Scenes**: Open the demo scenes to see the characters in action!
3. **URP/Built-in Setup**: Package materials import by default as URP (Models will appear pink on Built-in), to convert to Built-in go to **URP&Built-in** folder and double click the **convert-to-BUILT-IN** package patch.
4. From **Prefabs** folder drag & drop any character to your own scene.


## Animations

- **Standard Characters**: A set of common animations suitable to most characters.
  - Walking (6)
  - Running (4)
  - Idle (6)
  - Dancing (5)
  - Warming Up
  - Construction tool usage (7): Drill, Hammer, Handsaw, Pipewrench, Screwdriver, Wrench, Swap tool.

## Scripts

**CityPeople Component Script**

This component script offers basic functionality to show the basic animations and switch palette material. 

**SwapAndToolController**

Located For the construction worker demonstrate how to periodically switch to a random tool and play the matching animation in a loop.

## Palette System and UV
![](images/PaletteTextures.png)
The characters UV have been mapped in a way to make easy the switching of palettes. These textures (acting as palettes) have a standardized structure with areas corresponding to different surfaces of the characters:

- Skin colors
- Hair colors
- Clothes colors
- Dark and Light details.

A single texture/material pair can be applied to all the characters in this package. And any texture/material can be applied to any character.

Free tool [DA Poly Paint](https://assetstore.unity.com/packages/tools/painting/da-polypaint-low-poly-customizer-251157) can be used to further modify the UV 'painting' by model with ease.

## SRP Support

Current version default to URP (Universal Render Pipeline).
Follow the steps to convert to Built-in or rollback to URP at any time.

1. Navigate to folder **CityPeople/URP&Built-in**
2. Double click one of the following packages to apply the patch:
- **Convert-to-BUILT-IN**
- **Convert-to-URP**

Note about HDRP: While not converter patch is provided, supporting HDRP would need a few standard steps. Using the material conversion wizard and adjusting lighting accordingly.

## Support

If you have any questions or need assistance:

- **Email**: [denys.almaral@gmail.com](mailto\:denys.almaral@gmail.com)
- **Website**: [DenysAlmaral.com](https://denysalmaral.com)
- **Forum**: [Github Discussions](https://github.com/piXelicidio/Chat/discussions/2)

---

Thank you for downloading **City People FREE Samples**. We hope these assets help you create engaging and fun Unity projects!
