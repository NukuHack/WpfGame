# VoidVenture - A WPF Platformer Adventure

[![WPF](https://img.shields.io/badge/Powered%20by-WPF-blue)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
[![C#](https://img.shields.io/badge/C%23-5.0+-brightgreen)](https://docs.microsoft.com/en-us/dotnet/csharp/)

A procedurally enhanced 2D platformer built with C# and WPF, featuring dynamic color systems and modular game architecture. Perfect for learning WPF game development patterns and image manipulation techniques.

Some basic C#, made for some testing and to have fun developing it.
- Most of the code and the entire readme was written by AI so don't expect much
- Some parts may not work or some information might be false ... just like usual AI

## 🚀 Jump To
- 🤖 [Key Features](#key-features)
- 🛠️ [Technical Highlights](#technical-highlights)
- 🎮 [How to Play](#how-to-play)
- 📦 [Getting Started](#getting-started)
- 🤝 [Contributing](#contributing)
- 🎨 [Acknowledgments](#acknowledgments)
- 📜 [License](#license)

---

## 🤖 Key Features

- **Dynamic Movement System**  
  Smooth 8-directional movement with:
  - WASD/Arrow key controls
  - Momentum-based gravity system
  - Collision detection against tilemaps
  - Directional sprite rotation

- **Advanced Color Engine**  
  Real-time palette swapping system that:
  - Converts images to indexed color formats
  - Maintains 256-color compatibility
  - Supports procedural color randomization

- **Persistent Save System**  
  - Multiple save file support
  - Data serialization with versioning
  - Save file import/export functionality
  - In-game save management UI

- **Modular Architecture**  
  Clean separation of concerns with:
  - `Player` physics/movement class
  - `MathUtils` helper math module
  - `Palette` color management system
  - `Saves` - planned saves
  - Some extra TMX map loading support

---

## 🛠️ Technical Highlights

**1. Collision Detection System**  
- Tile-based collision using `Rect` intersections with dynamic bounds checking.

**2. Palette-Based Recoloring**  
- Advanced image processing pipeline.

**3. Game Loop Management**  
- 60 FPS update cycle using WPF DispatcherTimer.  
- This is important - if the game can not run at 60 fps the gravity and other movement might break.

---

## 🎮 How to Play

1. **Movement**:  
   - Arrow keys or WASD to move  
   - Spacebar for hover ability  
   - ESC to open menu

2. **Menu Controls**:  
   - Load/Save/Delete game progress  
   - Import external save files  
   - Customize game settings

---

## 📦 Getting Started

**1. Clone the repository**  
```bash
git clone https://github.com/NukuHack/WpfGame.git
```

**2. Requirements**  
- Visual Studio 2022+ with .NET 5.0 SDK
- WPF development workload installed

**3. Run the game**  
- You could try running the `.exe` file in the `VoidVenture\bin\Debug`, but there is a chance I did not compile it to the latest version  
  in that caseOpen `VoidVenture.sln` and press F5 to build/run

---

## 🤝 Contributing

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add new feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Open a pull request

Looking for help with:  
✅ Procedural generation
✅ Sound system implementation  
✅ Performance optimizations  
✅ macOS/Linux compatibility layer

---

## 📚 Documentation

- [WPF Documentation](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
- [TMX Map Format](https://doc.mapeditor.org/en/stable/reference/tmx-map-format/)
- [.NET Image Processing Guide](https://docs.microsoft.com/en-us/dotnet/desktop/winforms/advanced/image-processing)

---

## 🎨 Acknowledgments

Special thanks to:  
- nothing yet ...


---

## 📜 License

[MIT License](LICENSE) - Free to use, modify, and distribute.  
*Most of the code (and this readme) are generated with assistance from AI tools*
