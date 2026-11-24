# Horloge mondiale

![C#](https://img.shields.io/badge/C%23-239120?logo=csharp&logoColor=white)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![WPF](https://img.shields.io/badge/WPF-Windows-0078D4?logo=windows&logoColor=white)

Application en **C#** (WPF, .NET 10) qui affiche l'heure de plusieurs villes du monde avec une interface moderne style Windows 11 (thème Fluent, clair/sombre automatique).

![Capture de l'interface](docs/screenshot.png)

## Fonctionnalités

- Horloge analogique et numérique pour la ville sélectionnée
- Cartes par ville : heure, date, décalage (« Demain, +8 h »), icône jour/nuit
- Ajout / suppression de villes (liste sauvegardée dans `%AppData%\ClockApp\villes.json`)
- Heure récupérée via l'API [timeapi.io](https://timeapi.io), resynchronisée toutes les 15 min
- Repli sur les fuseaux horaires de Windows si l'API est injoignable

## Lancer

Nécessite Windows et le SDK .NET 10.

- **Visual Studio 2026** : ouvrir `ClockApp.slnx` puis lancer le projet.
- **En ligne de commande** :

  ```
  dotnet run
  ```
