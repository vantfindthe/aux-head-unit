"""Stands in for BeamNG to test AuxHeadUnit.exe without the game.

Sends the mod's 'state' packets for a few seconds, collects 'np' and chunked
'art' packets, then goes quiet so the companion should restore app volumes.
With a command (toggle, next, prev, play, pause; - for none) it presses that button halfway.
The source is pc unless given (bt for the phone).
Usage: py -3 fake_game.py [seconds] [volume] [command] [source]
"""
import base64, json, socket, sys, time

COMPANION = ('127.0.0.1', 47811)
seconds = float(sys.argv[1]) if len(sys.argv) > 1 else 3
volume = float(sys.argv[2]) if len(sys.argv) > 2 else 0.5
command = sys.argv[3] if len(sys.argv) > 3 and sys.argv[3] != '-' else None
source = sys.argv[4] if len(sys.argv) > 4 else 'pc'

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
sock.bind(('127.0.0.1', 47812))
sock.setblocking(False)

have_art = -1
art_parts, art_id = {}, None
np = None
end = time.time() + seconds
press_at = time.time() + seconds / 2
while time.time() < end:
    state = dict(t='state', power=True, ign=True, hasCar=True, inside=True, dist=0.5,
                 vol=volume, src=source, outside='realistic', haveArt=have_art)
    sock.sendto(json.dumps(state).encode(), COMPANION)
    if command and time.time() >= press_at:
        print('pressing ' + command)
        sock.sendto(json.dumps(dict(t='cmd', c=command)).encode(), COMPANION)
        command = None
    try:
        while True:
            data, _ = sock.recvfrom(65535)
            if len(data) > 8192:
                print('PACKET TOO BIG FOR THE GAME: %d bytes' % len(data))
            msg = json.loads(data)
            if msg['t'] == 'np':
                np = msg
            elif msg['t'] == 'art':
                if msg['id'] != art_id:
                    art_id, art_parts = msg['id'], {}
                art_parts[msg['part']] = msg['data']
                if len(art_parts) == msg['parts']:
                    jpeg = base64.b64decode(''.join(art_parts[i] for i in range(1, msg['parts'] + 1)))
                    print('art id %d: %d chunks, %d bytes, jpeg=%s' % (art_id, msg['parts'], len(jpeg), jpeg[:2] == b'\xff\xd8'))
                    have_art = art_id
    except BlockingIOError:
        pass
    except ConnectionResetError:
        pass
    time.sleep(0.05)

if np:
    bt = np['bt']
    if np.get('tier') == 'free':
        print('now playing (free companion, no song info): %s | playing=%s' % (np['app'], np['playing']))
    else:
        print('now playing: %s | %s - %s | playing=%s pos=%.1f/%.1f artId=%d' % (
            np['app'], np['artist'], np['title'], np['playing'], np['pos'], np['dur'], np['artId']))
    print('bluetooth: %s | devices: %s' % (bt['status'], ', '.join(d['name'] for d in bt['devices'])))
else:
    print('NO now-playing packets received')
sock.close()
