import { ArcType, BillboardGraphics, CallbackProperty, Cartesian3, Color, ConstantProperty, Entity, PolylineArrowMaterialProperty, Viewer } from 'cesium';

// Direction comes only from supplied positions, never from the illustrative orbit.
export function motionDirection(previous: Cartesian3, current: Cartesian3): Cartesian3 | undefined {
  const delta = Cartesian3.subtract(current, previous, new Cartesian3());
  return Cartesian3.magnitudeSquared(delta) > 0.000001 ? Cartesian3.normalize(delta, delta) : undefined;
}

function satelliteImage(): HTMLCanvasElement {
  const canvas = document.createElement('canvas');
  canvas.width = 120; canvas.height = 84;
  const ctx = canvas.getContext('2d')!;
  ctx.scale(3, 3);
  ctx.lineJoin = 'round'; ctx.lineWidth = 1.2;
  ctx.strokeStyle = '#08101c'; ctx.fillStyle = '#b9d6ed';
  ctx.fillRect(4, 6, 11, 16); ctx.strokeRect(4, 6, 11, 16);
  ctx.fillRect(25, 6, 11, 16); ctx.strokeRect(25, 6, 11, 16);
  ctx.strokeStyle = '#607d99'; ctx.lineWidth = 0.6;
  for (const x of [7.7, 11.3, 28.7, 32.3]) { ctx.beginPath(); ctx.moveTo(x, 6); ctx.lineTo(x, 22); ctx.stroke(); }
  for (const y of [10, 14, 18]) { ctx.beginPath(); ctx.moveTo(4, y); ctx.lineTo(15, y); ctx.moveTo(25, y); ctx.lineTo(36, y); ctx.stroke(); }
  ctx.strokeStyle = '#08101c'; ctx.lineWidth = 1.2;
  ctx.fillStyle = '#eef3fa'; ctx.fillRect(15, 9, 10, 10); ctx.strokeRect(15, 9, 10, 10);
  ctx.beginPath(); ctx.moveTo(20, 9); ctx.lineTo(20, 3); ctx.stroke();
  ctx.fillStyle = '#e7c885'; ctx.fillRect(17, 11, 6, 6);
  return canvas;
}

export class SatelliteMarkers {
  private readonly image = satelliteImage();
  private readonly states = new Map<string, { position: Cartesian3; epoch: number; direction?: Cartesian3; arrow: Entity }>();
  constructor(private readonly viewer: Viewer) {}
  clear() { this.states.clear(); }
  update(key: string, marker: Entity, position: Cartesian3, epoch: number, color: Color, next?: Cartesian3) {
    marker.point = undefined;
    if (!marker.billboard) marker.billboard = new BillboardGraphics({ image: this.image, width: 36, height: 25.2, color });
    marker.billboard!.color = new ConstantProperty(color);
    let state = this.states.get(key);
    if (!state) {
      const arrow = this.viewer.entities.add({ parent: marker.parent, show: false, polyline: {
        positions: [position, position], width: 9, arcType: ArcType.NONE,
        material: new PolylineArrowMaterialProperty(color),
      } });
      state = { position, epoch, arrow };
      this.states.set(key, state);
      const current = state;
      // A short world-space arrow: camera rotation naturally changes its projection.
      arrow.polyline!.positions = new CallbackProperty(() => {
        const length = Cartesian3.distance(this.viewer.camera.positionWC, current.position) * 0.045;
        const direction = current.direction ?? Cartesian3.ZERO;
        return [0.35, 1].map(fraction => Cartesian3.add(current.position,
          Cartesian3.multiplyByScalar(direction, length * fraction, new Cartesian3()), new Cartesian3()));
      }, false);
    }
    const elapsed = epoch - state.epoch;
    state.direction = next ? motionDirection(position, next)
      : elapsed > 0 && elapsed <= 3500 ? motionDirection(state.position, position) : undefined;
    state.position = position; state.epoch = epoch;
    state.arrow.show = !!state.direction;
    state.arrow.polyline!.material = new PolylineArrowMaterialProperty(color);
  }
}
