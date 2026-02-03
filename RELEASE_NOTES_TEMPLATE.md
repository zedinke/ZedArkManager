# Release Notes Template

Használd ezt a sablont minden új release létrehozásakor!

## Frissítési jegyzetek (Patch Notes)

### Új funkciók:
- [ ] Funkció 1
- [ ] Funkció 2

### Javítások:
- [ ] Javítás 1
- [ ] Javítás 2

### Módosítások:
- [ ] Módosítás 1
- [ ] Módosítás 2

### Ismert problémák:
- [ ] Probléma 1 (ha van)

---

**Példa release notes létrehozásához:**
```bash
gh release create v1.0.X ZedASAManager-1.0.X.zip --title "Version 1.0.X" --notes-file RELEASE_NOTES_TEMPLATE.md
```

Vagy közvetlenül a notes szövegével:
```bash
gh release create v1.0.X ZedASAManager-1.0.X.zip --title "Version 1.0.X" --notes "## Frissítési jegyzetek

### Új funkciók:
- Funkció leírása

### Javítások:
- Javítás leírása"
```
