# El huerto de los abuelos

Reconstrucción 3D en Unity de un huerto familiar real de La Alberca (Salamanca), recorrible a pie y con un **gemelo digital** que simula sus cultivos día a día con datos de clima reales. Es el proyecto de la asignatura de CG.

El terreno, los árboles y las casas del pueblo salen de datos abiertos (LiDAR del PNOA y Catastro), y la disposición del huerto se ha reconstruido a partir de fotos tomadas en la parcela.

## Contenido

- [Características](#características)
- [Requisitos](#requisitos)
- [Puesta en marcha](#puesta-en-marcha)
- [Controles](#controles)
- [Gemelo digital](#gemelo-digital)
- [Reconstrucción de la escena](#reconstrucción-de-la-escena)
- [Sonido](#sonido)
- [Estructura del proyecto](#estructura-del-proyecto)
- [Herramientas externas](#herramientas-externas)
- [Datos y licencias](#datos-y-licencias)

## Características

- **Escena a escala real**: parcela de 872 m² con su relieve, sus muros de piedra seca y sus objetos, rodeada por el pueblo y la sierra.
- **Agricultor jugable** en tercera o primera persona: anda, corre, salta y se encarama a los muros.
- **Siete bancales simulados**, cada uno con su cultivo y su propio escenario climático.
- **Tableta dentro del juego** para consultar los bancales, regar, cosechar, plantar, cambiar el clima y exportar los resultados.
- **Paisaje sonoro original** que reacciona a la simulación: las aves, los insectos y la lluvia dependen de la fecha y del clima del bancal más cercano.

## Requisitos

| Herramienta | Versión o nota |
|---|---|
| Unity | **6000.6.3f1**, con Universal Render Pipeline 17.6 e Input System 1.20 (se instalan con el proyecto) |
| Git LFS | Imprescindible: modelos, texturas, audio y terrenos se guardan por LFS |
| Python 3 | Solo para regenerar datos, texturas o audio ([más abajo](#herramientas-externas)) |

## Puesta en marcha

1. Instala Git LFS **antes de clonar**. Sin él, los binarios se descargan como punteros de texto y los assets aparecen rotos:

   ```bash
   git lfs install
   git clone --branch huerto-de-los-abuelos https://github.com/AlejPagar/Proyecto-CG.git
   ```

2. Abre la carpeta del proyecto desde Unity Hub con el editor 6000.6.3f1. La primera importación crea la carpeta `Library` y puede tardar bastante.
3. Abre `Assets/Escenas/Huerto.unity` y pulsa **Play**.

Para compilar el juego, usa **File > Build Profiles**. `Huerto.unity` es la única escena activada en la compilación. La carpeta `Build/` está ignorada por git.

## Controles

| Acción | Teclado y ratón | Mando |
|---|---|---|
| Andar | WASD o flechas | Stick izquierdo |
| Correr | Shift | Pulsar stick izquierdo o gatillo derecho |
| Saltar | Espacio | Botón inferior |
| Encaramarse a un muro | Empujar contra el muro | Empujar contra el muro |
| Mirar | Ratón (clic para capturarlo, Esc para soltarlo) o mantener el botón derecho | Stick derecho |
| Acercar o alejar la cámara | Rueda | |
| Primera o tercera persona | V | |
| Abrir o cerrar la tableta | Tab | Select |
| Quitar o poner la música | M | |
| Mostrar u ocultar la ayuda | H o F1 | |

## Gemelo digital

El simulador avanza por la temporada del 17 de marzo al 30 de noviembre y da un paso diario en cada bancal. El reloj va de 0,1 a 15 días simulados por segundo y también permite saltar una semana o un mes.

### Bancales

| Nº | Situación | Cultivo | Escenario de clima inicial |
|---|---|---|---|
| 1 | Bancal alto, junto al cerezo | Tomate | Año típico (2021) |
| 2 | Bancal alto, centro | Pimiento | Ola de calor en julio |
| 3 | Bancal alto, bajo la roca | Patata | Año lluvioso (2025) |
| 4 | Bancal alto, lindero este | Calabaza | Cambio climático (+2 °C) |
| 5 | Bancal medio, oeste | Judía verde | Helada tardía en mayo |
| 6 | Bancal medio, este | Cebolla | Año seco (2005) |
| 7 | Bajo el castaño, junto a la caseta | Lechuga | Sombra de los árboles |

La berza es el octavo cultivo del catálogo: no empieza en ningún bancal, pero se puede plantar desde la tableta.

### Modelo de cultivo

`ModeloCultivo.cs` calcula para cada bancal, cada día:

- **Desarrollo por grados-día**: la temperatura se acota entre la base y el tope de cada cultivo. Los grados-día totales están calibrados con los 30 años de datos, entre las fechas habituales de siembra y cosecha en la sierra.
- **Balance de agua del suelo** según FAO-56, con coeficiente de cultivo (Kc), agua útil y coeficiente de estrés (Ks). El suelo es jabre, con unos 120 mm de agua útil por metro de profundidad.
- **Vigor**: la biomasa relativa, que crece con el desarrollo si hay agua, luz y salud.
- **Salud**: baja con las heladas, el calor extremo, la sed, el encharcamiento y las enfermedades, y se recupera despacio.
- **Efectos propios de algunos cultivos**: mildiu (tomate, patata y cebolla), oídio (calabaza), caída de flores por calor y espigado de la lechuga.
- **Cosecha estimada**, a partir del rendimiento del cultivo, la superficie, el vigor, el cuajado, la salud y las pérdidas por cosechar tarde.

### Clima

La base es la serie diaria de Open-Meteo (reanálisis ERA5) de 1996 a 2025 en el huerto, en `Assets/Huerto/Datos/Meteo_LaAlberca_1996_2025.csv`. Cada bancal usa uno de estos dos tipos de año:

- **Real**: cualquier año de la serie.
- **Sintético**: generado con la estadística de esos 30 años, con una cadena de Markov para los días de lluvia y anomalías de temperatura correlacionadas de un día a otro.

Sobre ese año se pueden aplicar cambios de temperatura y de lluvia, sombra de los árboles, ola de calor, helada tardía y sequía de verano.

### Tableta

| Pestaña | Qué ofrece |
|---|---|
| **Estado** | Situación del bancal, riego de 15 mm, riego por goteo automático, cosecha y plantación de otro cultivo |
| **Clima** | Escenario, año de datos, ajustes de temperatura, lluvia y sombra, y episodios extremos |
| **Historial** | Gráficas de la temporada del bancal |
| **Comparar** | Tabla con todos los bancales: cultivo, clima, estado, desarrollo, salud, humedad y cosecha |
| **Sonido** | Volúmenes y cambio de tema musical |

El botón **Exportar CSV** guarda el historial de todos los bancales en `Application.persistentDataPath`, en un archivo `Huerto_temporada_<fecha>.csv`.

Los resultados también se ven en la escena: las plantas cambian de malla según su etapa, las hojas amarillean al perder salud, el suelo se oscurece con la humedad y solo llueve sobre el bancal cuando llueve en su serie.

## Reconstrucción de la escena

La escena se genera por código desde el menú **Huerto** del editor. Las coordenadas son metros locales (x al este, z al norte) y la altura es la altitud real menos 1040 m.

| Elemento | Cómo se construye |
|---|---|
| **Huerto** | Terreno de 192 m con muestras cada 0,1875 m, a partir del LiDAR PNOA 2025, con los escalones de los bancales |
| **Pueblo** | Terreno de 1024 m con resolución de 1 m |
| **Sierra** | Terreno lejano de 10 km |
| **Casas** | Planta de Catastro, con alturas y tejados ajustados a la nube LiDAR, y arquitectura serrana procedural: granito, entramado, balcones y teja árabe |
| **Muros** | Piedra seca colocada piedra a piedra, cubierta de hiedra |
| **Árboles** | Posiciones del LiDAR. Los cercanos usan modelos procedurales o de la Asset Store; los lejanos se funden en una malla por cada cuadro de 250 m para reducir el coste de CPU |
| **Iluminación** | HDRI de Poly Haven girado para que su sol coincida con el de las fotos, niebla exponencial y postproceso con tonemapping ACES y bloom |
| **Cuenca visual** | Se lanzan rayos desde los puntos donde puede estar la cámara del jugador, y no se genera lo que ninguno alcanza |

### Menú Huerto

| Opción | Qué hace |
|---|---|
| **Generar escena completa** | Reconstruye `Assets/Escenas/Huerto.unity` desde cero |
| **Calcular cuenca visual y regenerar** | Recalcula lo que se ve desde el huerto y regenera la escena |
| **Añadir sonido a la escena** | Ajusta la importación del audio y monta el objeto de sonido |
| **Terceros > Extraer animaciones del agricultor** | Prepara las animaciones del personaje |

## Sonido

Toda la música y los efectos del huerto se han sintetizado con numpy, sin grabaciones ni muestras de terceros. Incluye tres temas originales, pisadas según la capa del terreno que se pisa (hierba, tierra, hojarasca o piedra), bucles de ambiente, cantos de aves, campanas y los sonidos de la tableta. Los detalles están en [`Assets/Huerto/Herramientas/Audio/LEEME.md`](Assets/Huerto/Herramientas/Audio/LEEME.md).

## Estructura del proyecto

```text
Assets/
├── Escenas/        Huerto.unity, y Terreno_Original y Entorno_Final (ejercicio del lago)
├── Huerto/
│   ├── Scripts/
│   │   ├── Juego/      Personaje, cámara, animación, sonido
│   │   ├── Gemelo/     Cultivos, clima, modelo, bancales, simulador, tableta
│   │   └── Editor/     Generadores de la escena (menú Huerto)
│   ├── Datos/          LiDAR procesado, Catastro, clima y cuenca visual
│   ├── Herramientas/   Scripts de Python (LiDAR, texturas, audio, paquetes)
│   ├── Modelos/        Mallas generadas, modelos importados y prefabs
│   ├── Terreno/        Terrenos del huerto, el pueblo y la sierra, y sus capas
│   └── Animacion/  Audio/  Materiales/  Texturas/  UI/
├── Lago_Nevado/    Primer ejercicio: terreno nevado con lago a partir de una foto
├── Terceros/       Paquetes de la Asset Store y animaciones de Mixamo
└── Settings/       Ajustes de URP y acciones del Input System
```

[`Assets/LEEME.md`](Assets/LEEME.md) describe cada carpeta con más detalle.

## Herramientas externas

Los datos, las texturas y el audio ya están en el repositorio, así que estos scripts solo hacen falta para regenerarlos:

| Carpeta | Uso | Dependencias |
|---|---|---|
| [`Herramientas/LiDAR`](Assets/Huerto/Herramientas/LiDAR/LEEME.md) | Procesar el LiDAR y Catastro, y crear texturas | `laspy[lazrs]`, `numpy`, `pillow`, `pyproj`, `scipy`, `opencv-python-headless` |
| `Herramientas/Texturas` | Crear texturas de plantas, de los carteles de los bancales y del castaño | `numpy`, `pillow` |
| [`Herramientas/Audio`](Assets/Huerto/Herramientas/Audio/LEEME.md) | Sintetizar la música y los efectos | `numpy`, `ffmpeg` |
| `Herramientas/Paquetes` | Extraer parte de un `.unitypackage` sin importarlo entero, conservando los GUID | Solo Python |

## Datos y licencias

| Recurso | Origen | Licencia |
|---|---|---|
| LiDAR y modelos del terreno | IGN / PNOA | CC BY 4.0 |
| Parcelas y edificios | Dirección General del Catastro | Según sus condiciones de uso |
| Clima 1996-2025 | Open-Meteo (ERA5 / ERA5-Land) | CC BY 4.0 |
| Texturas, HDRI, rocas y ramas | Poly Haven | CC0 |
| Personaje, árboles, aperos, cestas, atrezo y cielos (`Assets/Terceros`) | Unity Asset Store | EULA de la Asset Store |
| Animaciones de tableta y de escalada | Mixamo | Condiciones de Adobe Mixamo |
| Música y efectos del huerto | Creación propia (síntesis) | |

> [!WARNING]
> La EULA de la Asset Store no permite publicar estos paquetes en un repositorio abierto. Este repositorio debe seguir siendo privado mientras los incluya: para hacerlo público habría que retirar antes `Assets/Terceros` del repositorio y de su historial.
