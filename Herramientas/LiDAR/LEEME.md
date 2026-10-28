# Scripts de preparación de datos

Scripts en Python con los que se generaron los datos de `Assets/Huerto/Datos` y algunas texturas.
Dependencias: `pip install "laspy[lazrs]" numpy pillow pyproj scipy opencv-python-headless`.

| Script | Qué hace |
|---|---|
| `prep_huerto.py` | Rejilla del huerto, parcelas de Catastro y ortofoto |
| `lidar_rasters.py`, `lidar_vista.py`, `lidar_escalones.py` | MDT del huerto (0,1875 m) y escalones de los bancales a partir del LiDAR PNOA 2025 |
| `lidar_extraer.py` | Extrae suelo, edificios, vegetación y color de las 4 teselas LiDAR |
| `lidar_pueblo.py` | MDT del pueblo (1 m), tejados por edificio, árboles y mapa de superficies |
| `filtrar_arboles.py` | Quita los árboles del LiDAR que caen dentro de las casas |
| `entramado.py`, `balcon.py` | Texturas del entramado, barandillas y geranios |
| `hojas.py`, `hojas_foto.py`, `hierba_foto.py` | Texturas de hojas e hierba a partir de fotos CC0 de Poly Haven |
| `overlay.py` | Superpone la escena y las fotos para comparar |

Los scripts esperan las rutas de entrada y salida como argumentos (ver el principio de cada uno).
Datos: LiDAR y MDT del IGN/PNOA (CC BY 4.0), cartografía de la Dirección General del Catastro, texturas de Poly Haven (CC0).
