# Audio del huerto

Toda la música y los sonidos se sintetizan con numpy (sin grabaciones ni muestras de terceros) y se guardan en OGG Vorbis con ffmpeg.

```
cd Assets/Huerto/Herramientas/Audio
python3 musica.py  ../../Audio/Musica
python3 sonidos.py ../../Audio
```

- `sintesis.py`: filtros por FFT, reverberación por convolución, cuerda pulsada (Karplus-Strong), flauta, celesta, colchón de cuerdas y campana.
- `musica.py`: tres temas originales («El huerto de los abuelos», «Tarde de verano», «Recuerdos»).
- `sonidos.py`: pisadas (hierba, tierra, hojarasca, piedra), salto, trepar, sonidos de la tableta y de las tareas, bucles de ambiente que empalman sin corte (viento, lluvia, chicharras, grillos, pájaros), cantos de aves y campanas.

En Unity, `Huerto > Añadir sonido a la escena` monta el objeto «Sonido» (también lo hace `Huerto > Generar escena completa`).
