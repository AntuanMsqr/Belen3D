# Belén — Demo Rápida (HCP + Cámara)

Este proyecto muestra una cámara con proyección off‑axis (Head‑Coupled Perspective) y control por pose de cabeza vía UDP.

Requisitos
- Windows con webcam (o archivo de vídeo)
- Unity (abre el proyecto normalmente)

Pasos (Demo con escena incluida)
1) Abrir la escena de ejemplo en Unity:
   - `Assets/Scenes/HcpTargetsParallaxSample.unity`
2) Ejecutar el tracker de cámara (OpenSeeFace) desde PowerShell/CMD en la raíz del repo:
   - `.
Tools\OpenSeeFace\Binary\facetracker.exe -c 0 -v 3 -P 1`
   - Consejos:
     - Listar cámaras: `.
Tools\OpenSeeFace\Binary\facetracker.exe -l 1`
     - Elegir otra cámara: cambia `-c 0` por el índice mostrado
3) En Unity, presiona Play para ver el efecto HCP.

Notas sobre el receptor en Unity (escenas incluidas)
- Las escenas de muestra (p. ej., `Assets/Scenes/HcpTargetsParallaxSample.unity`) ya están configuradas para OpenSeeFace: incluyen un GameObject `OpenSeeReceiver` con los componentes `OpenSee.OpenSee` y `OpenSeeFaceBoxSource`.
- Por ello, usa el tracker OpenSeeFace (`facetracker.exe` como arriba). No necesitas `UdpHeadPoseReceiver` para estas escenas.
- Si deseas usar el receptor genérico JSON (`UdpHeadPoseReceiver`), cambia la fuente en tu escena y utiliza la alternativa “MediaPipe JSON” de abajo.

Alternativas de entrada (opcionales)
- Sin cámara (prueba rápida):
  - `py Tools/headpose_dummy_sender.py`
  - Envía un movimiento sinusoidal a `127.0.0.1:11573` (JSON)
- MediaPipe JSON (para `UdpHeadPoseReceiver`):
  1) Instalar dependencias: `py -m pip install opencv-python mediapipe numpy`
  2) Ejecutar: `py Tools/mediapipe_headpose_udp.py --cam 0 --host 127.0.0.1 --port 11573 --show`

Sugerencias de calibración
- Coloca el `ScreenCenter` en el plano real de la pantalla y define `screenWidth`/`screenHeight` en metros.
- Ajusta `neutralZ` (≈0.6–1.0 m) y filtros en `FaceTrackerManager` para suavidad.
- Si ves la profundidad invertida, invierte Z en tu fuente o en el receptor (`invertZ`).

Problemas comunes
- “Cámara ocupada”: cierra apps que usen la webcam y vuelve a lanzar el tracker.
- Sin datos en Unity: verifica puerto 11573, firewall y que el receptor correcto esté en la escena.
