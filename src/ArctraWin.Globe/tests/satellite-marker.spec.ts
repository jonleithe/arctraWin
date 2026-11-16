import { test, expect } from '@playwright/test';
import { Cartesian3 } from 'cesium';
import { motionDirection } from '../src/satellite-marker';

test('motion arrows follow supplied Earth-fixed movement, including reversed and stationary states', () => {
  const first = new Cartesian3(12_000_000, 0, 0);
  const next = new Cartesian3(12_000_000, 3000, 4000);
  expect(motionDirection(first, next)).toEqual(new Cartesian3(0, 0.6, 0.8));
  expect(motionDirection(next, first)).toEqual(new Cartesian3(0, -0.6, -0.8));
  expect(motionDirection(first, first)).toBeUndefined();
});
