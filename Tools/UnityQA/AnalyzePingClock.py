"""Summarize PingClockRuntimeQA CSV files without changing gameplay."""
import csv, json, pathlib, statistics, sys

root = pathlib.Path(sys.argv[1])
def rows(file):
    with file.open(encoding='utf-8-sig', newline='') as stream:
        return list(csv.DictReader(stream))
def median(data):
    return statistics.median(data) if data else None
def rate(frames, hidden):
    pairs = [(a, b) for a, b in zip(frames, frames[1:]) if int(a['hidden']) == hidden == int(b['hidden'])]
    wall = sum(float(b['wall_delta']) for _, b in pairs)
    return sum(float(b['timer_delta']) for _, b in pairs) / wall if wall else None
summary = []
for file in sorted(root.glob('*-frames.csv')):
    role = file.name.removesuffix('-frames.csv')
    frames, pings, submission = rows(file), rows(root / (role+'-pings.csv')), rows(root / (role+'-submission.csv'))
    if not frames:
        continue
    pings = [p for p in pings if float(p['wall']) >= float(frames[0]['wall'])-3]
    send = next((s for s in submission if s['stage']=='send'), {})
    ack = next((s for s in submission if s['stage']=='ack'), {})
    result = next((s for s in rows(root / 'results.csv') if s['name']==role), {})
    config = dict(line.split('=', 1) for line in (root / (role+'-config.txt')).read_text().splitlines())
    extras = [(float(f['timer_delta'])-float(f['wall_delta']))*1000 for f in frames[1:]]
    probes = rows(root / 'host-probes.csv')
    probe = next((p for p in probes if p['name']==role), {})
    item = dict(role=role, frame_count=len(frames), target_s=int(config['target_cs'])/100,
        hide_s=float(config['hide_s']), display_mismatches=int(config['display_mismatches']),
        ranking_observed=config.get('ranking_observed'),
        utp_rtt_ms=median([float(p['utp_ms']) for p in pings]),
        app_rtt_ms=median([float(p['app_rtt_ms']) for p in pings]),
        clock_lag_ms=median([float(p['server_clock_lag_ms']) for p in pings]),
        visible_rate=rate(frames,0), hidden_rate=rate(frames,1),
        max_forward_adjust_ms=max(extras), max_backward_adjust_ms=min(extras),
        sample_age_ms=float(probe['sample_age_ms']) if probe else None,
        sent_seconds=float(send['elapsed_or_record']) if send else None,
        accepted=bool(int(ack['value_or_rtt'])) if ack else None,
        acknowledged_cs=int(ack['elapsed_or_record']) if ack else None,
        result_cs=int(result['record_cs']) if result else None,
        lives=int(result['lives']) if result else None)
    if item['accepted']:
        item['record_error_ms']=(item['acknowledged_cs']/100-item['sent_seconds'])*1000
    summary.append(item)
(root / 'summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
for x in summary:
    fmt = lambda v: '-' if v is None else f'{v:.1f}'
    print(f"{x['role']}: RTT={fmt(x['utp_rtt_ms'])}ms, age={fmt(x['sample_age_ms'])}ms, "
          f"rate={x['visible_rate']:.4f}/{x['hidden_rate']:.4f}, "
          f"jump={x['max_forward_adjust_ms']:.1f}/{x['max_backward_adjust_ms']:.1f}ms, "
          f"accepted={x['accepted']}, result={x['result_cs']}, lives={x['lives']}, UI mismatch={x['display_mismatches']}")
