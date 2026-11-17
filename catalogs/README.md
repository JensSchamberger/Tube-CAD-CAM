# Profilkataloge

Dieses Verzeichnis enthält die **mitgelieferten Profilkataloge** (`*.json`). Derzeit ist **kein Katalog installiert**:
Normbezeichnungen und -abmessungen dürfen nicht erfunden werden und können hier nicht gegen die jeweilige Norm
geprüft werden. Die Bibliothek ist deshalb leer, bis ein geprüfter Katalog hinzugefügt wird.

## Katalog hinzufügen
Eine `*.json`-Datei in dieses Verzeichnis legen (wird beim Start automatisch gelesen). Format:

```json
{
  "SchemaVersion": 1,
  "Name": "Name des Katalogs",
  "Entries": [
    {
      "Designation": "<Bezeichnung laut Norm>",
      "Standard": "<Norm, Ausgabe>",
      "Source": "<Tabelle/Dokument, aus dem die Maße stammen>",
      "Verified": false,
      "Profile": { "$profile": "round", "OuterDiameterMm": 0.0, "WallThicknessMm": 0.0 }
    }
  ]
}
```

* `Standard` und `Source` sind Pflicht. `Verified` darf nur `true` sein, wenn die Maße von einer Person gegen die
  Quelle geprüft wurden; unverifizierte Einträge werden in der Oberfläche mit ⚠ markiert.
* Bezeichnungen müssen eindeutig sein (Groß-/Kleinschreibung egal). Doppelte Einträge werden abgelehnt, nie überschrieben.
* Profiltypen (`$profile`): `round` (`OuterDiameterMm`, `WallThicknessMm`), `rectangular` (`WidthMm`, `HeightMm`,
  `WallThicknessMm`, `OuterCornerRadiusMm`; Quadratrohr = gleiche Breite/Höhe), `u` und `i` (`HeightMm`, `FlangeWidthMm`,
  `WebThicknessMm`, `FlangeThicknessMm`), `l` (`LegAMm`, `LegBMm`, `ThicknessMm`), `custom` (`Name`, `Outer`, `Voids`).
* Alle Maße in Millimetern. Das Beispiel oben enthält absichtlich Platzhalterwerte und ist **kein** Katalogeintrag.

Eigene Profile speichert die Anwendung im Benutzerverzeichnis (`%AppData%\TubeCadCam\user-profiles.json`) mit
`Standard = "user-defined"` und `Verified = false`.
