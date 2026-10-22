/**
 * QuizMS Gamification & Interaction Engine
 * Native canvas confetti, audio synth chimes, and timer pulse effects.
 * 100% dependency-free, production-grade micro-interactions.
 */
(function(window) {
    'use strict';

    var QuizCelebration = {};

    // 1. Native Canvas Confetti Cannon
    QuizCelebration.confetti = function(options) {
        options = options || {};
        var duration = options.duration || 3000;
        var particleCount = options.particleCount || 70;
        var colors = options.colors || ['#7c6df8', '#22d3ee', '#fbbf24', '#34d399', '#f43f5e', '#ffffff'];

        var canvas = document.createElement('canvas');
        canvas.style.position = 'fixed';
        canvas.style.top = '0';
        canvas.style.left = '0';
        canvas.style.width = '100vw';
        canvas.style.height = '100vh';
        canvas.style.pointerEvents = 'none';
        canvas.style.zIndex = '999999';
        document.body.appendChild(canvas);

        var ctx = canvas.getContext('2d');
        if (!ctx) return;

        var width = canvas.width = window.innerWidth;
        var height = canvas.height = window.innerHeight;

        var particles = [];
        for (var i = 0; i < particleCount; i++) {
            particles.push({
                x: width * 0.5 + (Math.random() - 0.5) * 200,
                y: height * 0.55,
                vx: (Math.random() - 0.5) * 16,
                vy: -(Math.random() * 14 + 10),
                gravity: 0.45,
                friction: 0.98,
                size: Math.random() * 8 + 5,
                rotation: Math.random() * 360,
                rotationSpeed: (Math.random() - 0.5) * 12,
                color: colors[Math.floor(Math.random() * colors.length)],
                opacity: 1,
                decay: Math.random() * 0.008 + 0.012
            });
        }

        var startTime = Date.now();
        function render() {
            ctx.clearRect(0, 0, width, height);
            var alive = false;

            for (var j = 0; j < particles.length; j++) {
                var p = particles[j];
                p.vx *= p.friction;
                p.vy *= p.friction;
                p.vy += p.gravity;
                p.x += p.vx;
                p.y += p.vy;
                p.rotation += p.rotationSpeed;
                p.opacity -= p.decay;

                if (p.opacity > 0 && p.y < height + 50) {
                    alive = true;
                    ctx.save();
                    ctx.translate(p.x, p.y);
                    ctx.rotate((p.rotation * Math.PI) / 180);
                    ctx.globalAlpha = Math.max(0, p.opacity);
                    ctx.fillStyle = p.color;
                    ctx.fillRect(-p.size / 2, -p.size / 2, p.size, p.size * 0.6);
                    ctx.restore();
                }
            }

            if (alive && (Date.now() - startTime) < duration) {
                requestAnimationFrame(render);
            } else {
                if (canvas.parentNode) {
                    canvas.parentNode.removeChild(canvas);
                }
            }
        }

        requestAnimationFrame(render);
    };

    // 2. Synthesized Victory Chime (Zero audio asset downloads needed)
    QuizCelebration.playVictoryChime = function() {
        try {
            var AudioContext = window.AudioContext || window.webkitAudioContext;
            if (!AudioContext) return;
            var ctx = new AudioContext();

            var notes = [523.25, 659.25, 783.99, 1046.50]; // C5, E5, G5, C6
            notes.forEach(function(freq, idx) {
                var osc = ctx.createOscillator();
                var gain = ctx.createGain();
                osc.type = 'triangle';
                osc.frequency.value = freq;
                gain.gain.setValueAtTime(0.08, ctx.currentTime + idx * 0.08);
                gain.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + idx * 0.08 + 0.5);
                osc.connect(gain);
                gain.connect(ctx.destination);
                osc.start(ctx.currentTime + idx * 0.08);
                osc.stop(ctx.currentTime + idx * 0.08 + 0.5);
            });
        } catch (e) {
            // Audio policy or un-interacted context; ignore safely
        }
    };

    window.QuizCelebration = QuizCelebration;

})(window);
