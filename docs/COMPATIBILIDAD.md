# Compatibilidad y validación de v1.0.0

## Objetivo exacto

- Jellyfin Server 12.4.0.0, .NET 10, Linux x64.
- Código Server tag `v12.4`, commit `83d0d0e7f76e23e2e085c8ff67d28b772d35967d`.
- Imagen oficial `jellyfin/jellyfin:12.4`, fijada por digest en el build.
- FFmpeg 8.1.3-Jellyfin original.

`targetAbi` del catálogo representa un mínimo para Jellyfin. La comprobación
interna de la DLL restringe este plugin exactamente a Server 12.4.0.0. No se
declara compatibilidad con versiones anteriores o posteriores.

## Validación realizada

Pruebas de contrato y de integración ejecutadas en un servidor aislado, con
HEVC Main 10 híbrido DV P8/compatibilidad 1 + HDR10+, sin EL, con base HDR10.
El repositorio de distribución contiene el producto y su build; el laboratorio,
las muestras privadas y los directorios de pruebas no se publican aquí.

| Salida solicitada | MPEG-TS | fMP4 |
|---|---|---|
| Eliminar DV, conservar HDR10+ | Verificado | Verificado |
| Eliminar HDR10+, conservar DV | Verificado | Verificado, incluyendo configuración DV del init |
| Eliminar ambos | Verificado | Verificado |

Verificaciones efectuadas:

- Ausencia/presencia de RPU real mediante extracción HEVC Annex B.
- Presencia/ausencia HDR10+ por fotogramas.
- Payloads VCL idénticos al original mediante SHA256, sin recodificación.
- Misma resolución y profundidad 10 bits; PQ, BT.2020 y BT.2020nc preservados.
- Mastering display y content light metadata preservados.
- Señalización HLS y configuración DV del init fMP4 coherentes con la salida.
- HDR10 puro y SDR sin modificar; HDR10+ procesado selectivamente.
- Plugin desactivado, Dry Run y clientes excluidos mantienen DirectPlay.
- Dos políticas congeladas simultáneas sin compartir salida HDR incorrecta.
- Saltos temporales, APIs de pausa/reanudación y cambio a pista E-AC-3/Atmos.
- SubRip embebido entregado externamente y subtítulos SRT externos.
- Restricciones de usuario, concurrencia, Stop y ausencia de FFmpeg huérfano.
- Fallo real de FFmpeg provocado: error, sin salida SDR ni retry propio.
- Página administrativa integrada con guardado persistente.

Se analizan fragmentos y segmentos; esto no equivale a verificar todos los
fotogramas de una película completa. Las fixtures HDR10/HDR10+ sin DV derivan
del clip híbrido, no constituyen un corpus independiente.

## Pendiente para aceptación completa

1. Wholphin 1.1.1 / ExoPlayer en Xiaomi Mi Box S y activación HDR en Hisense.
   No se ha dispuesto físicamente del dispositivo. Conservar HDR10+ quitando
   solo DV no demuestra que el televisor vaya a activar HDR con ese cliente.
2. Muestras reales Profile 5 y 7. Su exclusión está verificada por contrato,
   pero no se ha ejecutado con un corpus real de ambos perfiles.
3. Muestra >=5 minutos para activar condiciones nativas de throttling y
   segment cleaner. El clip principal disponible dura tres minutos.
4. Carga sostenida, abandono sin Stop, reinicios durante streaming y
   coexistencia con otros plugins que intervengan en PlaybackInfo/HLS/DI.

## Exclusiones actuales

DV P5/P7, P8.2, HLG, señalización HDR10 incompleta, enhancement layer,
fuentes remotas/live TV, múltiples pistas de vídeo y burn-in. Las respuestas
con varias fuentes requieren una fuente seleccionada explícitamente.

El plugin no convierte HDR dinámico a SDR ni modifica los originales.
