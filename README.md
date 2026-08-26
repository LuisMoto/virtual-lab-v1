# VR Optical Physics Simulator

**Immersive Virtual Reality Simulation for Quantum and Optical Physics**

Built by Luis Moto and Nathalia Jazmín Ballesteros Luna — equal contributors.

## Project Summary

A virtual reality system for simulating optical physics experiments in a highly immersive environment. Traditional physics simulators rely on 2D web interfaces featuring static buttons and graphs, which fail to teach the tactile and spatial skills required in a real laboratory.

This project addresses that limitation through a hybrid architecture combining an interactive VR front-end with a high-performance physics back-end. The platform allows students to manually assemble and interact with:
* Safe Content (e.g., standard optical alignments)
* Laser emitters and photodetectors.
* Beam splitters and half-wave plates (HWP).
* The Grangier experiment setup.

The system supports real-time manipulation of optical components, generating live photon counts and wave interference calculations via WebSockets, mimicking the behavior of highly expensive physical laboratory equipment.

## The Problem

The project explores a recurring technical and educational challenge in quantum physics:
How can expensive, fragile, and inaccessible optical laboratory equipment be effectively simulated using Virtual Reality?

Key questions explored during development:
* Can a VR environment accurately train students on the spatial assembly of optical tables and the consequences of incorrect setups?
* How does a continuous, stateful WebSocket connection bridge a C# Unity interface with a Python-based physical calculation engine in real-time?
* How can a strict, enterprise-grade monorepo architecture ensure scalable collaboration between frontend, backend, and 3D modeling teams?

## Tools and Technologies

| Category | Tools / Methods |
| :--- | :--- |
| **Frontend & VR** | Unity, C#, XR Interaction Toolkit, Newtonsoft.Json |
| **Backend & Physics** | Python, FastAPI, WebSockets |
| **3D Modeling & Art** | Blender, PBR Materials, Universal Render Pipeline (URP) |
| **Version Control & Architecture** | Git, Monorepo, Singleton Pattern, DTO Mapping |

## Directory Structure

The project is organized as follows to ensure modularity and reproducibility:

.
├── README.md
├── .gitignore
│
├── Assets/                             # Unity Client (Frontend & VR)
│   ├── Materials/                      # PBR Materials (M_ prefix)
│   ├── Models/                         # 3D FBX Models (SM_ prefix)
│   ├── Prefabs/                        # Assembled VR interactables (PF_ prefix)
│   ├── Scripts/                        # C# Architecture
│   │   ├── Controllers/                # Scene and Simulation logic
│   │   ├── Models/                     # DTOs mapped to snake_case
│   │   ├── Utils/                      # Helpers and Volumetric Lines
│   │   └── XR/                         # Socket and Grab interactors
│   ├── Textures/                       # Base, Normal, and Metallic maps (T_ prefix)
│   └── Scenes/                         # Main Unity levels (e.g., Scene_DosDet)
│
├── Backend/                            # Python Server (Physics Engine)
│   ├── simulator.py                    # Grangier experiment logic
│   ├── wave_simulator.py               # Wave interference calculations
│   ├── server.py                       # FastAPI & WebSocket implementation
│   └── requirements.txt                # Python dependencies
│
└── Docs/
    └── ESTANDARES_DOCUMENTACION.md     # v2.0.0 Naming and Architecture Guide
