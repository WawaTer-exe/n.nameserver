# n.NameServers 🚀

`n.NameServers` is a modern, high-utility open-source private server emulation engine written in C# (ASP.NET Core) tailored specifically for 2020 Rec Room IL2CPP game clients. 

This project goes beyond traditional offline sandbox emulators by intercepting the client's discovery handshake layer and dynamically injecting live internet-facing Photon Cloud configuration vectors. It turns an old client build into an active multiplayer playground with persistent profiles, custom item databases, and localized voice mechanics.

---

## ✨ Core Components & Architecture

### 1. Dynamic Authentication & Onboarding
* **Auto-Registration:** The server tracks incoming unique machine device footprints or connection signature headers. If an identity is not found on disk, the server immediately triggers the in-game account registration flow.
* **Username Conflict Protection:** Fully maps out `POST /api/players/v1/v2/create`. If a new user attempts to claim a handle already registered to another player, the backend handles the duplication validation, dropping an explicit warning message directly into the game client interface.

### 2. Isolated Storage Engine
* **Private Profile State Layouts:** Saves statistics, levels, progression, and token wallet balances inside individual `.json` configuration profiles located under `NameServerStorage/Profiles/`.
* **Personal Wardrobes:** All dressing room mirror custom configurations save strictly inside independent user inventory templates (`NameServerStorage/Avatars/{playerId}.json`) to completely prevent cosmetic overrides between players.

### 3. Maker Pen Room Storage
* **Sandbox Data Streaming:** Captures watch creator canvas signals (`POST /api/rooms/v4/create`) and converts incoming byte array pushes into persistent binary data files on disk (`NameServerStorage/SavedRooms/`). Hit **Save Room** in your watch menu to permanently retain maps.

### 4. High-Performance Social Matrix
* **Server-Wide Friending:** Automatically matches every unique profile registered on your host node as mutual friends on the social lookup tree. 
* **Party Up Group Tracker:** Synchronizes real-time invite handshakes (`ActiveParties`). When the party leader loads into a game mode, the server commands all linked instances to transition into the exact same instance space.

### 5. Multi-User Network Relay
* **Live Photon Integration:** Integrates real global Photon PUN structural routing keys alongside native voice processing application ids to establish fully synchronized online movements and spatial 3D localized chat over port `5055`.

---

## 🛠️ Getting Started

### Prerequisites
Make sure you have the [.NET 8 SDK](https://microsoft.com) installed on your computer.

### Installation and Launch
1. Clone or download this repository onto your machine.
2. Open your terminal or command prompt inside the project folder directory.
3. Build and execute the software stack by running:
   ```bash
   dotnet run
   ```
4. **Keep this terminal window running actively!** The server will host its data channels locally over custom community port `20592`.

---

## 🎮 Game Client Patching Instructions

Because your 2020 client assembly is compiled natively using Unity's IL2CPP binary pipeline, destination domains are hard-coded into data arrays. To route your client game file directly into this alternative host engine:

1. Download a free hexadecimal binary file inspector application like **HxD**.
2. Navigate down your game's directory tree to find the metadata matrix folder:
   `RecRoom_Data / il2cpp_data / Metadata / global-metadata.dat`
3. Drag and drop `global-metadata.dat` into HxD.
4. Press `Ctrl + F`, choose the **Text-string** search tab, and find: `https://rec.net` (or `https://rec.net`).
5. Overwrite the character array precisely by typing your target server port: `http://localhost:20592`.
6. **Critical Pointer Alignment Rule:** Your new local address contains fewer total string characters than the old official game URL. **Never hit backspace or delete to remove leftover trailing letters!** Doing so breaks file alignment sizes, corrupts structural pointer indexes, and causes an immediate game engine crash on boot.
7. Instead, click on the residual trailing letters and manually override their specific hexadecimal values in the left-hand column to `00` (Null bytes) until the old text strings disappear completely.
8. Save the modifications (`Ctrl + S`).
9. **Launch Note:** Ensure your official desktop Steam client application is running logged into your account in the background before opening the game. This satisfies underlying library ticket handshake checks (`steam_api64.dll`) so your client initializes smoothly.

---

## 🔒 Open Source Licensing

This project is open-source software distributed under the terms of the **MIT License**. Feel free to fork, expand features, fix modules, or re-distribute files cleanly across your network space.
