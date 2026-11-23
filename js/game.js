/**
 * ============================================================================
 * ECHO: Sonar Escape — Complete SLAM Robotics Simulation & Game Engine
 * ============================================================================
 * Concepts Demonstrated:
 * 1. LiDAR/Sonar Rangefinding: 180-ray radial raymarching
 * 2. Occupancy Grid Mapping: 0 = Unknown, 1 = Free, 2 = Occupied (Wall)
 * 3. Pose Localization: Player state estimation x_t = [x, y]^T
 * 4. Acoustic Adversarial Motion Planning: Grid BFS shortest path tracking
 * 5. Procedural Audio Synthesis: Web Audio API sonar pings and proximity radar
 * ============================================================================
 */

(() => {
  'use strict';

  // --- Configuration & Constants ---
  const TILE = 40;
  const RAYS = 180;

  const DIFFICULTIES = {
    easy: {
      cols: 15,
      rows: 11,
      range: 260,
      cooldown: 1.0,
      monsterSpeed: 50,
      label: 'Easy'
    },
    medium: {
      cols: 21,
      rows: 15,
      range: 220,
      cooldown: 1.5,
      monsterSpeed: 68,
      label: 'Medium'
    },
    hard: {
      cols: 31,
      rows: 19,
      range: 160,
      cooldown: 2.2,
      monsterSpeed: 88,
      label: 'Hard'
    }
  };

  let currentDiff = 'medium';
  let COLS = DIFFICULTIES[currentDiff].cols;
  let ROWS = DIFFICULTIES[currentDiff].rows;
  let RANGE = DIFFICULTIES[currentDiff].range;
  let COOLDOWN = DIFFICULTIES[currentDiff].cooldown;
  let MONSTER_SPEED = DIFFICULTIES[currentDiff].monsterSpeed;

  // --- DOM Elements ---
  const canvas = document.getElementById('game');
  const ctx = canvas.getContext('2d');
  const viewport = document.getElementById('viewport');
  const scareOverlay = document.getElementById('scare-overlay');

  // HUD Elements
  const statExplored = document.getElementById('stat-explored');
  const chargeBar = document.getElementById('charge-bar');
  const statDanger = document.getElementById('stat-danger');
  const statTime = document.getElementById('stat-time');
  const btnAudio = document.getElementById('btn-audio');
  const btnSlamInfo = document.getElementById('btn-slam-info');
  const btnCloseSlam = document.getElementById('btn-close-slam');
  const btnDismissSlam = document.getElementById('btn-dismiss-slam');

  // Modals
  const modalContainer = document.getElementById('modal-container');
  const gameModal = document.getElementById('game-modal');
  const slamModal = document.getElementById('slam-modal');
  const modalTitle = document.getElementById('modal-title');
  const modalDesc = document.getElementById('modal-desc');
  const btnPrimary = document.getElementById('btn-primary');
  const diffBtns = document.querySelectorAll('.diff-btn');
  const runStats = document.getElementById('run-stats');
  const runTime = document.getElementById('run-time');
  const runMapped = document.getElementById('run-mapped');
  const runPulses = document.getElementById('run-pulses');
  const runBest = document.getElementById('run-best');

  // Mobile Controls
  const btnPulse = document.getElementById('btn-pulse');
  const dbtns = document.querySelectorAll('.dbtn');

  // --- Game State Variables ---
  let maze = null;        // 2D Array: 0 = floor, 1 = wall
  let known = null;       // 2D Occupancy Grid: 0 = unknown, 1 = free, 2 = wall
  let player = null;      // { x, y, speed: 140, angle: 0 }
  let exitCell = null;    // { r, c, seen: 0 }
  let monster = null;     // { x, y, speed, path: [] }
  let hits = [];          // Echo points: [{ x, y, t }]
  let pulseRing = null;   // { x, y, t }
  let lastPulse = -99;
  let pulseCount = 0;
  let totalFloorCells = 0;
  let state = 'menu';     // 'menu' | 'play' | 'over'
  let startTime = 0;
  let finalDuration = 0;

  // --- Audio Synthesis via Web Audio API ---
  let actx = null;
  let isMuted = false;
  let monsterOsc = null;
  let monsterGain = null;

  function initAudio() {
    if (!actx) {
      const AudioCtx = window.AudioContext || window.webkitAudioContext;
      if (AudioCtx) {
        actx = new AudioCtx();
        setupMonsterDrone();
      }
    }
    if (actx && actx.state === 'suspended') {
      actx.resume();
    }
  }

  function setupMonsterDrone() {
    if (!actx) return;
    try {
      monsterOsc = actx.createOscillator();
      monsterGain = actx.createGain();
      monsterOsc.type = 'sawtooth';
      monsterOsc.frequency.setValueAtTime(55, actx.currentTime); // Low A1 rumble

      // Low pass filter for muffled menacing tone
      const filter = actx.createBiquadFilter();
      filter.type = 'lowpass';
      filter.frequency.setValueAtTime(140, actx.currentTime);

      monsterGain.gain.setValueAtTime(0.0001, actx.currentTime);

      monsterOsc.connect(filter);
      filter.connect(monsterGain);
      monsterGain.connect(actx.destination);
      monsterOsc.start();
    } catch (e) {
      console.warn('Audio setup error:', e);
    }
  }

  function updateMonsterAudio(distance) {
    if (!actx || !monsterGain || isMuted || state !== 'play') {
      if (monsterGain) monsterGain.gain.setTargetAtTime(0.0001, actx ? actx.currentTime : 0, 0.05);
      return;
    }
    const maxAuditRange = 380;
    if (distance < maxAuditRange) {
      // Proximity intensity: closer = louder and slightly higher pitch
      const factor = 1 - Math.max(0, distance / maxAuditRange);
      const targetGain = Math.min(0.28, factor * factor * 0.35);
      const targetFreq = 50 + factor * 60; // 50Hz to 110Hz

      monsterGain.gain.setTargetAtTime(targetGain, actx.currentTime, 0.08);
      monsterOsc.frequency.setTargetAtTime(targetFreq, actx.currentTime, 0.08);
    } else {
      monsterGain.gain.setTargetAtTime(0.0001, actx.currentTime, 0.1);
    }
  }

  function playSonarPing() {
    if (!actx || isMuted) return;
    try {
      const t = actx.currentTime;
      const osc = actx.createOscillator();
      const gain = actx.createGain();

      osc.type = 'sine';
      osc.frequency.setValueAtTime(950, t);
      osc.frequency.exponentialRampToValueAtTime(180, t + 0.65);

      gain.gain.setValueAtTime(0.45, t);
      gain.gain.exponentialRampToValueAtTime(0.0001, t + 0.7);

      osc.connect(gain);
      gain.connect(actx.destination);
      osc.start(t);
      osc.stop(t + 0.7);
    } catch (e) {
      console.warn('Audio ping error:', e);
    }
  }

  function playWinSound() {
    if (!actx || isMuted) return;
    try {
      const t = actx.currentTime;
      const notes = [440, 554.37, 659.25, 880]; // A major chord
      notes.forEach((freq, idx) => {
        const osc = actx.createOscillator();
        const gain = actx.createGain();
        osc.frequency.setValueAtTime(freq, t + idx * 0.12);
        gain.gain.setValueAtTime(0.3, t + idx * 0.12);
        gain.gain.exponentialRampToValueAtTime(0.0001, t + idx * 0.12 + 0.6);
        osc.connect(gain);
        gain.connect(actx.destination);
        osc.start(t + idx * 0.12);
        osc.stop(t + idx * 0.12 + 0.65);
      });
    } catch (e) {}
  }

  function playLoseSound() {
    if (!actx || isMuted) return;
    try {
      const t = actx.currentTime;
      const osc = actx.createOscillator();
      const gain = actx.createGain();
      osc.type = 'sawtooth';
      osc.frequency.setValueAtTime(180, t);
      osc.frequency.exponentialRampToValueAtTime(35, t + 0.7);

      gain.gain.setValueAtTime(0.6, t);
      gain.gain.exponentialRampToValueAtTime(0.001, t + 0.75);

      osc.connect(gain);
      gain.connect(actx.destination);
      osc.start(t);
      osc.stop(t + 0.75);
    } catch (e) {}
  }

  // --- Maze Generation (Recursive Backtracker) ---
  function makeMaze(cols, rows) {
    const grid = Array.from({ length: rows }, () => Array(cols).fill(1));
    const stack = [[1, 1]];
    grid[1][1] = 0;

    while (stack.length > 0) {
      const [x, y] = stack[stack.length - 1];
      const dirs = [
        [2, 0], [-2, 0], [0, 2], [0, -2]
      ].sort(() => Math.random() - 0.5);

      let moved = false;
      for (const [dx, dy] of dirs) {
        const nx = x + dx;
        const ny = y + dy;
        if (nx > 0 && ny > 0 && nx < cols - 1 && ny < rows - 1 && grid[ny][nx] === 1) {
          grid[y + dy / 2][x + dx / 2] = 0;
          grid[ny][nx] = 0;
          stack.push([nx, ny]);
          moved = true;
          break;
        }
      }
      if (!moved) stack.pop();
    }

    // Ensure exit cell and player cell are clear
    grid[1][1] = 0;
    grid[rows - 2][cols - 2] = 0;
    return grid;
  }

  // --- Reset & Initialization ---
  function resetGame() {
    COLS = DIFFICULTIES[currentDiff].cols;
    ROWS = DIFFICULTIES[currentDiff].rows;
    RANGE = DIFFICULTIES[currentDiff].range;
    COOLDOWN = DIFFICULTIES[currentDiff].cooldown;
    MONSTER_SPEED = DIFFICULTIES[currentDiff].monsterSpeed;

    canvas.width = COLS * TILE;
    canvas.height = ROWS * TILE;

    maze = makeMaze(COLS, ROWS);
    known = Array.from({ length: ROWS }, () => Array(COLS).fill(0));

    // Calculate total traversable floor cells for exploration %
    totalFloorCells = 0;
    for (let r = 0; r < ROWS; r++) {
      for (let c = 0; c < COLS; c++) {
        if (maze[r][c] === 0) totalFloorCells++;
      }
    }

    player = {
      x: TILE * 1.5,
      y: TILE * 1.5,
      speed: 135,
      angle: 0
    };

    exitCell = {
      r: ROWS - 2,
      c: COLS - 2,
      seen: 0
    };

    // Monster starts at far top-right corner
    monster = {
      x: TILE * (COLS - 1.5),
      y: TILE * 1.5,
      speed: MONSTER_SPEED,
      path: []
    };

    hits = [];
    pulseRing = null;
    lastPulse = -99;
    pulseCount = 0;
    startTime = performance.now() / 1000;
    state = 'play';

    modalContainer.classList.add('hidden');
    scareOverlay.classList.add('hidden');
    viewport.classList.remove('shake');
  }

  // --- Movement & Wall Collision Handling ---
  const keys = {};

  window.addEventListener('keydown', e => {
    const k = e.key.toLowerCase();
    keys[k] = true;
    if (e.code === 'Space') {
      pulse();
      e.preventDefault();
    }
    if (k === 'm') {
      toggleAudio();
    }
  });

  window.addEventListener('keyup', e => {
    keys[e.key.toLowerCase()] = false;
  });

  // Mobile Touch Controls
  dbtns.forEach(btn => {
    const dir = btn.getAttribute('data-dir');
    const startPress = (e) => { e.preventDefault(); keys[dir] = true; };
    const endPress = (e) => { e.preventDefault(); keys[dir] = false; };
    btn.addEventListener('touchstart', startPress, { passive: false });
    btn.addEventListener('touchend', endPress, { passive: false });
    btn.addEventListener('mousedown', startPress);
    btn.addEventListener('mouseup', endPress);
    btn.addEventListener('mouseleave', endPress);
  });

  btnPulse.addEventListener('touchstart', (e) => { e.preventDefault(); pulse(); }, { passive: false });
  btnPulse.addEventListener('click', pulse);

  function isWall(x, y) {
    const c = Math.floor(x / TILE);
    const r = Math.floor(y / TILE);
    return maze[r]?.[c] !== 0;
  }

  function movePlayer(dt) {
    const dx = (keys['d'] || keys['arrowright'] ? 1 : 0) - (keys['a'] || keys['arrowleft'] ? 1 : 0);
    const dy = (keys['s'] || keys['arrowdown'] ? 1 : 0) - (keys['w'] || keys['arrowup'] ? 1 : 0);

    if (dx !== 0 || dy !== 0) {
      player.angle = Math.atan2(dy, dx);
    }

    const step = player.speed * dt;
    const nx = player.x + dx * step;
    const ny = player.y + dy * step;
    const pad = 8; // Collision boundary padding to prevent clipping

    const okX = !isWall(nx + Math.sign(dx) * pad, player.y - pad / 2) &&
                !isWall(nx + Math.sign(dx) * pad, player.y + pad / 2);
    const okY = !isWall(player.x - pad / 2, ny + Math.sign(dy) * pad) &&
                !isWall(player.x + pad / 2, ny + Math.sign(dy) * pad);

    if (okX) player.x = nx;
    if (okY) player.y = ny;
  }

  // --- Sonar Pulse (Simulated 2D LiDAR Rangefinder & SLAM Update) ---
  function pulse() {
    initAudio();
    const now = performance.now() / 1000;
    if (state !== 'play' || (now - lastPulse < COOLDOWN)) return;

    lastPulse = now;
    pulseCount++;
    pulseRing = { x: player.x, y: player.y, t: now };

    // Cast 180 radial rays across 360 degrees
    for (let i = 0; i < RAYS; i++) {
      const angle = (i / RAYS) * Math.PI * 2;
      const cosA = Math.cos(angle);
      const sinA = Math.sin(angle);

      // Raymarching: step outward in 3px increments
      for (let d = 0; d < RANGE; d += 3) {
        const rx = player.x + cosA * d;
        const ry = player.y + sinA * d;
        const c = Math.floor(rx / TILE);
        const r = Math.floor(ry / TILE);

        // Ray collision with wall boundary
        if (maze[r]?.[c] !== 0) {
          hits.push({ x: rx, y: ry, t: now });
          if (known[r] && known[r][c] !== undefined) {
            known[r][c] = 2; // Mark as occupied / wall in occupancy grid
          }
          break; // Stop ray on wall impact
        }

        // Ray passing through open navigable space
        if (known[r]?.[c] === 0) {
          known[r][c] = 1; // Mark cell as explored free space
        }

        // Check if exit tile is illuminated by ray
        if (r === exitCell.r && c === exitCell.c) {
          exitCell.seen = now;
        }
      }
    }

    // Acoustic noise source alerts the monster
    const noiseCell = {
      r: Math.floor(player.y / TILE),
      c: Math.floor(player.x / TILE)
    };
    monster.path = bfs(cellOf(monster), noiseCell);

    playSonarPing();
  }

  // --- Breadth-First Search (BFS) Adversarial Pathfinding ---
  const cellOf = (pos) => ({
    r: Math.floor(pos.y / TILE),
    c: Math.floor(pos.x / TILE)
  });

  function bfs(start, goal) {
    if (!start || !goal) return [];
    if (start.r === goal.r && start.c === goal.c) return [];

    const key = n => `${n.r},${n.c}`;
    const prev = { [key(start)]: null };
    const queue = [start];

    while (queue.length > 0) {
      const cur = queue.shift();
      if (cur.r === goal.r && cur.c === goal.c) break;

      const neighbors = [
        { r: cur.r + 1, c: cur.c },
        { r: cur.r - 1, c: cur.c },
        { r: cur.r, c: cur.c + 1 },
        { r: cur.r, c: cur.c - 1 }
      ];

      for (const next of neighbors) {
        const k = key(next);
        if (maze[next.r]?.[next.c] === 0 && !(k in prev)) {
          prev[k] = cur;
          queue.push(next);
        }
      }
    }

    if (!(key(goal) in prev)) return [];

    const path = [];
    for (let at = goal; at; at = prev[key(at)]) {
      path.unshift(at);
    }
    path.shift(); // Remove starting tile
    return path;
  }

  function moveMonster(dt) {
    // If monster has reached noise target or has no path, patrol to a random open cell
    if (!monster.path || monster.path.length === 0) {
      const openCells = [];
      for (let r = 1; r < ROWS - 1; r += 2) {
        for (let c = 1; c < COLS - 1; c += 2) {
          if (maze[r]?.[c] === 0) openCells.push({ r, c });
        }
      }
      if (openCells.length > 0) {
        const randTarget = openCells[Math.floor(Math.random() * openCells.length)];
        monster.path = bfs(cellOf(monster), randTarget);
      }
      return;
    }

    const nextTile = monster.path[0];
    const targetX = (nextTile.c + 0.5) * TILE;
    const targetY = (nextTile.r + 0.5) * TILE;

    const dx = targetX - monster.x;
    const dy = targetY - monster.y;
    const dist = Math.hypot(dx, dy);

    if (dist < 3) {
      monster.path.shift();
      return;
    }

    monster.x += (dx / dist) * monster.speed * dt;
    monster.y += (dy / dist) * monster.speed * dt;
  }

  // --- Rendering Engine ---
  function draw(now) {
    // Clear Canvas to pitch black
    ctx.fillStyle = '#020406';
    ctx.fillRect(0, 0, canvas.width, canvas.height);

    // 1. Draw Occupancy Grid Memory (SLAM Map)
    for (let r = 0; r < ROWS; r++) {
      for (let c = 0; c < COLS; c++) {
        const cellState = known[r][c];
        const cellX = c * TILE;
        const cellY = r * TILE;

        if (cellState === 2) {
          // Known Wall: Faint cyan boundary with cyber outline
          ctx.fillStyle = 'rgba(0, 255, 204, 0.12)';
          ctx.fillRect(cellX, cellY, TILE, TILE);
          ctx.strokeStyle = 'rgba(0, 255, 204, 0.22)';
          ctx.lineWidth = 1;
          ctx.strokeRect(cellX + 0.5, cellY + 0.5, TILE - 1, TILE - 1);
        } else if (cellState === 1) {
          // Known Free Space: Very faint translucent navigable grid
          ctx.fillStyle = 'rgba(255, 255, 255, 0.025)';
          ctx.fillRect(cellX, cellY, TILE, TILE);
        }
      }
    }

    // 2. Draw Fresh Echo Points (Decaying LiDAR hits)
    hits = hits.filter(h => now - h.t < 3.0);
    for (const h of hits) {
      const age = now - h.t;
      const alpha = Math.max(0, 1 - age / 3.0);
      ctx.fillStyle = `rgba(0, 255, 204, ${alpha})`;
      ctx.shadowColor = '#00ffcc';
      ctx.shadowBlur = 6 * alpha;
      ctx.fillRect(h.x - 2, h.y - 2, 4, 4);
    }
    ctx.shadowBlur = 0;

    // 3. Expanding Sonar Wavefront Ring
    if (pulseRing && (now - pulseRing.t < 0.75)) {
      const progress = (now - pulseRing.t) / 0.75;
      const currentRadius = progress * RANGE;
      const ringAlpha = Math.max(0, 1 - progress);

      ctx.strokeStyle = `rgba(0, 255, 204, ${ringAlpha * 0.85})`;
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.arc(pulseRing.x, pulseRing.y, currentRadius, 0, Math.PI * 2);
      ctx.stroke();
    }

    // 4. Exit Portal (Illuminated for 4.0 seconds if scanned)
    if (exitCell.seen && (now - exitCell.seen < 4.0)) {
      const exitProgress = (now - exitCell.seen) / 4.0;
      const exitAlpha = Math.max(0.1, 1 - exitProgress);
      const ex = exitCell.c * TILE + 6;
      const ey = exitCell.r * TILE + 6;
      const es = TILE - 12;

      ctx.shadowColor = '#00ff7f';
      ctx.shadowBlur = 15;
      ctx.fillStyle = `rgba(0, 255, 127, ${exitAlpha * 0.9})`;
      ctx.fillRect(ex, ey, es, es);

      ctx.strokeStyle = '#ffffff';
      ctx.lineWidth = 2;
      ctx.strokeRect(ex, ey, es, es);
      ctx.shadowBlur = 0;
    }

    // 5. Monster Reveal Blip (If in close proximity or caught in pulse wave)
    const distToMonster = Math.hypot(player.x - monster.x, player.y - monster.y);
    if (distToMonster < 90) {
      const blipAlpha = Math.min(0.9, (90 - distToMonster) / 90);
      ctx.fillStyle = `rgba(255, 30, 60, ${blipAlpha})`;
      ctx.beginPath();
      ctx.arc(monster.x, monster.y, 7, 0, Math.PI * 2);
      ctx.fill();

      // Glowing red eyes
      ctx.fillStyle = '#fff';
      ctx.beginPath();
      ctx.arc(monster.x - 3, monster.y - 2, 1.5, 0, Math.PI * 2);
      ctx.arc(monster.x + 3, monster.y - 2, 1.5, 0, Math.PI * 2);
      ctx.fill();
    }

    // 6. Player Representation (Robot Node with Heading Direction)
    ctx.shadowColor = '#00ffcc';
    ctx.shadowBlur = 10;
    ctx.fillStyle = '#ffffff';
    ctx.beginPath();
    ctx.arc(player.x, player.y, 6, 0, Math.PI * 2);
    ctx.fill();

    // Directional orientation notch (robot localization heading)
    ctx.strokeStyle = '#00ffcc';
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.moveTo(player.x, player.y);
    ctx.lineTo(player.x + Math.cos(player.angle) * 11, player.y + Math.sin(player.angle) * 11);
    ctx.stroke();
    ctx.shadowBlur = 0;
  }

  // --- HUD Updates ---
  function updateHUD(now) {
    if (state !== 'play') return;

    // 1. Exploration Percentage
    let exploredCount = 0;
    for (let r = 0; r < ROWS; r++) {
      for (let c = 0; c < COLS; c++) {
        if (known[r][c] === 1) exploredCount++;
      }
    }
    const percent = Math.min(100, Math.round((exploredCount / totalFloorCells) * 100));
    statExplored.textContent = `${percent}%`;

    // 2. Pulse Cooldown Charge Bar
    const cdProgress = Math.min(1, (now - lastPulse) / COOLDOWN);
    chargeBar.style.width = `${Math.round(cdProgress * 100)}%`;
    if (cdProgress >= 1) {
      chargeBar.style.background = 'linear-gradient(90deg, #00ffaa, #00ffcc)';
      btnPulse.style.opacity = '1';
    } else {
      chargeBar.style.background = 'linear-gradient(90deg, #557766, #779988)';
      btnPulse.style.opacity = '0.65';
    }

    // 3. Proximity Danger Radar
    const dist = Math.hypot(player.x - monster.x, player.y - monster.y);
    updateMonsterAudio(dist);

    if (dist < 110) {
      statDanger.textContent = 'CRITICAL';
      statDanger.className = 'hud-value danger-critical';
    } else if (dist < 230) {
      statDanger.textContent = 'DETECTED';
      statDanger.className = 'hud-value danger-warn';
    } else {
      statDanger.textContent = 'CALM';
      statDanger.className = 'hud-value danger-safe';
    }

    // 4. Time Counter
    const elapsed = Math.floor(now - startTime);
    const mins = String(Math.floor(elapsed / 60)).padStart(2, '0');
    const secs = String(elapsed % 60).padStart(2, '0');
    statTime.textContent = `${mins}:${secs}`;
  }

  // --- Win / Loss End Game ---
  function endGame(won) {
    state = 'over';
    finalDuration = performance.now() / 1000 - startTime;

    updateMonsterAudio(9999); // Stop monster audio rumble

    // Format final time string
    const mins = String(Math.floor(finalDuration / 60)).padStart(2, '0');
    const secs = String(Math.floor(finalDuration % 60)).padStart(2, '0');
    const timeFormatted = `${mins}:${secs}`;

    // Calculate final explored %
    let exploredCount = 0;
    for (let r = 0; r < ROWS; r++) {
      for (let c = 0; c < COLS; c++) {
        if (known[r][c] === 1) exploredCount++;
      }
    }
    const finalPercent = Math.min(100, Math.round((exploredCount / totalFloorCells) * 100));

    // Handle Best Time storage
    const bestKey = `echo_best_${currentDiff}`;
    let bestTime = localStorage.getItem(bestKey);
    if (won) {
      if (!bestTime || finalDuration < parseFloat(bestTime)) {
        localStorage.setItem(bestKey, finalDuration.toFixed(1));
        bestTime = finalDuration.toFixed(1);
      }
    }
    const bestTimeDisplay = bestTime ? `${parseFloat(bestTime).toFixed(1)}s` : '--';

    // Populate stats panel
    runTime.textContent = timeFormatted;
    runMapped.textContent = `${finalPercent}%`;
    runPulses.textContent = String(pulseCount);
    runBest.textContent = bestTimeDisplay;
    runStats.classList.remove('hidden');

    if (won) {
      playWinSound();
      modalTitle.textContent = 'SIGNAL ESCAPED';
      modalTitle.style.color = '#00ff7f';
      modalDesc.innerHTML = `You successfully localized the exit portal and escaped the dark labyrinth in <strong>${timeFormatted}</strong>!`;
      btnPrimary.textContent = 'PLAY AGAIN';
      modalContainer.classList.remove('hidden');
    } else {
      playLoseSound();
      // Jumpscare screen shake and flash
      viewport.classList.add('shake');
      scareOverlay.classList.remove('hidden');

      setTimeout(() => {
        scareOverlay.classList.add('hidden');
        viewport.classList.remove('shake');
        modalTitle.textContent = 'SIGNAL TERMINATED';
        modalTitle.style.color = '#ff3366';
        modalDesc.innerHTML = `The creature tracked your acoustic pulses and intercepted your location.`;
        btnPrimary.textContent = 'RETRY SIMULATION';
        modalContainer.classList.remove('hidden');
      }, 900);
    }
  }

  // --- Main Animation & Simulation Loop ---
  let lastTimestamp = performance.now();

  function loop(timestamp) {
    const dt = Math.min((timestamp - lastTimestamp) / 1000, 0.05);
    lastTimestamp = timestamp;
    const now = timestamp / 1000;

    if (state === 'play') {
      movePlayer(dt);
      moveMonster(dt);

      // Check Exit Condition
      const playerCell = cellOf(player);
      if (playerCell.r === exitCell.r && playerCell.c === exitCell.c) {
        endGame(true);
      }

      // Check Monster Collision Condition
      const distToMonster = Math.hypot(player.x - monster.x, player.y - monster.y);
      if (distToMonster < 15) {
        endGame(false);
      }

      updateHUD(now);
    }

    if (maze) {
      draw(now);
    }

    requestAnimationFrame(loop);
  }

  // --- UI Event Handlers ---
  function toggleAudio() {
    isMuted = !isMuted;
    btnAudio.textContent = isMuted ? '🔇' : '🔊';
    btnAudio.title = isMuted ? 'Unmute Audio (M)' : 'Mute Audio (M)';
    if (isMuted && monsterGain && actx) {
      monsterGain.gain.setValueAtTime(0.0001, actx.currentTime);
    }
  }

  btnAudio.addEventListener('click', toggleAudio);

  // Difficulty selection buttons
  diffBtns.forEach(btn => {
    btn.addEventListener('click', () => {
      diffBtns.forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      currentDiff = btn.getAttribute('data-diff');
    });
  });

  // Start / Play Again Button
  btnPrimary.addEventListener('click', () => {
    initAudio();
    resetGame();
  });

  // SLAM Educational Modal
  btnSlamInfo.addEventListener('click', () => {
    slamModal.classList.remove('hidden');
    gameModal.classList.add('hidden');
    modalContainer.classList.remove('hidden');
  });

  function closeSlamModal() {
    slamModal.classList.add('hidden');
    if (state === 'play') {
      modalContainer.classList.add('hidden');
    } else {
      gameModal.classList.remove('hidden');
    }
  }

  btnCloseSlam.addEventListener('click', closeSlamModal);
  btnDismissSlam.addEventListener('click', closeSlamModal);

  // Start the render loop
  requestAnimationFrame(loop);

})();
