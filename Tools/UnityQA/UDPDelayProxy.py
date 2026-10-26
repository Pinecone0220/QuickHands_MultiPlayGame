"""Loopback-only UDP delay relay for Unity transport tests; leaves game/packages untouched."""
import argparse, heapq, json, pathlib, random, selectors, socket, time

p = argparse.ArgumentParser()
p.add_argument('--listen', type=int, required=True)
p.add_argument('--target', type=int, required=True)
p.add_argument('--delay', type=float, default=0)
p.add_argument('--jitter', type=float, default=0)
p.add_argument('--switch-marker')
p.add_argument('--stop', required=True)
p.add_argument('--stats', required=True)
a = p.parse_args()
front, back = socket.socket(socket.AF_INET, socket.SOCK_DGRAM), socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
front.bind(('127.0.0.1', a.listen)); back.bind(('127.0.0.1', 0))
sel = selectors.DefaultSelector()
for s in (front, back):
    s.setblocking(False); sel.register(s, selectors.EVENT_READ)
queue, client = [], None
seed, serial, counts = random.Random(a.listen), 0, {'up': 0, 'down': 0}
started, switched = time.perf_counter(), None
try:
    while time.perf_counter() - started < 180 and not pathlib.Path(a.stop).exists():
        now = time.perf_counter()
        if switched is None and a.switch_marker and pathlib.Path(a.switch_marker).exists():
            switched = now
        delay = a.delay if switched is None else (350 if now - switched < 2 else 50)
        while queue and queue[0][0] <= now:
            _, _, sock, data, dest = heapq.heappop(queue)
            try:
                sock.sendto(data, dest)
            except ConnectionResetError:
                pass
        timeout = min(.01, max(0, queue[0][0] - now)) if queue else .01
        for key, _ in sel.select(timeout):
            while True:
                try:
                    data, addr = key.fileobj.recvfrom(65535)
                except BlockingIOError:
                    break
                except ConnectionResetError:
                    continue
                if key.fileobj is front:
                    client = addr; sock, dest, direction = back, ('127.0.0.1', a.target), 'up'
                elif client is not None:
                    sock, dest, direction = front, client, 'down'
                else:
                    continue
                due = time.perf_counter() + max(0, delay + seed.uniform(-a.jitter, a.jitter)) / 1000
                serial += 1; counts[direction] += 1
                heapq.heappush(queue, (due, serial, sock, data, dest))
finally:
    counts.update(delay_ms=a.delay, jitter_ms=a.jitter, switched=switched is not None, seconds=time.perf_counter()-started)
    pathlib.Path(a.stats).write_text(json.dumps(counts), encoding='utf-8')
    sel.close(); front.close(); back.close()
