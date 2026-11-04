# Proyecto CG: el huerto de los abuelos

Estructura de `Assets`:

| Carpeta | Contenido |
|---|---|
| `Escenas/` | `Huerto.unity` (proyecto de la asignatura), `Terreno_Original.unity` y `Entorno_Final.unity` (ejercicio del lago nevado) |
| `Huerto/Scripts/Juego/` | Personaje (`Paseante`, `CamaraPaseante`, `AnimacionAgricultor`), sonido (`AudioHuerto`) y utilidades de juego |
| `Huerto/Scripts/Gemelo/` | Gemelo digital: cultivos, clima, modelo de crecimiento, bancales, simulador, tableta y graficas |
| `Huerto/Scripts/Editor/` | Generadores de la escena (menu **Huerto**): terreno, muros, casas, arboles, objetos, cultivos, personaje y sonido |
| `Huerto/Modelos/Generados/` | Mallas que crean los generadores, por tipo: Arboles, Bosque, Casas, Muros, Cultivos, Objetos, Personaje |
| `Huerto/Modelos/Importados/` | Rocas y ramas de Poly Haven (FBX, CC0) |
| `Huerto/Modelos/Prefabs/` | Prefabs de los arboles del bosque |
| `Huerto/Texturas/`, `Huerto/Materiales/` | Texturas (fotos del huerto, Poly Haven, generadas) y materiales URP |
| `Huerto/Terreno/` | Datos de los tres terrenos (huerto, pueblo, sierra) y sus capas en `Capas/` |
| `Huerto/Animacion/` | Animaciones y controlador del agricultor |
| `Huerto/Audio/` | Musica, ambiente y efectos (sintetizados con `Herramientas/Audio`) |
| `Huerto/UI/` | Estilos y panel de la tableta (UI Toolkit) |
| `Huerto/Datos/` | LiDAR del PNOA ya procesado, Catastro, clima 1996-2025 y cuenca visual |
| `Huerto/Herramientas/` | Scripts de Python: LiDAR, texturas, audio y extraccion de paquetes de la Asset Store |
| `Lago_Nevado/` | Primer ejercicio: foto de referencia, terreno, texturas, agua y arboles nevados |
| `Terceros/` | Paquetes gratuitos de la Asset Store (agricultor, arboles, aperos, cestas, cielos) y la plantilla de URP |
| `Settings/` | Ajustes de URP y acciones del Input System |

Los paquetes de `Terceros/` tienen la licencia EULA de la Asset Store: no se pueden publicar en un repositorio abierto.
