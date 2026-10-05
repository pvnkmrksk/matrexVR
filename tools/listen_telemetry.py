#!/usr/bin/env python3
"""Subscribe to MATREX's separate ZMQ telemetry channel (requires pyzmq)."""
import argparse
import json
import sys


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--endpoint', default='tcp://127.0.0.1:9880')
    parser.add_argument('--topic', default='matrex.telemetry.v1')
    parser.add_argument('--count', type=int, default=0, help='stop after N snapshots; 0 means forever')
    parser.add_argument('--timeout', type=float, default=0, help='exit if no snapshot arrives within this many seconds; 0 waits forever')
    parser.add_argument('--pretty', action='store_true', help='indent JSON; default is one JSON object per line')
    args = parser.parse_args()
    if args.count < 0 or args.timeout < 0:
        parser.error('count and timeout must be nonnegative')
    try:
        import zmq
    except ImportError:
        parser.error('pyzmq is required: python3 -m pip install pyzmq')
    with zmq.Context() as context:
        with context.socket(zmq.SUB) as socket:
            socket.setsockopt(zmq.LINGER, 0)
            socket.setsockopt(zmq.RCVHWM, 10)
            socket.setsockopt_string(zmq.SUBSCRIBE, args.topic)
            socket.connect(args.endpoint)
            received = 0
            try:
                while args.count == 0 or received < args.count:
                    if args.timeout and not socket.poll(round(args.timeout * 1000)):
                        print('No telemetry received before timeout.', file=sys.stderr)
                        return 1
                    frames = socket.recv_multipart()
                    if len(frames) != 2 or frames[0].decode('utf-8') != args.topic:
                        continue
                    data = json.loads(frames[1])
                    if data.get('schemaVersion') != 1:
                        raise ValueError('Unsupported telemetry schemaVersion')
                    print(json.dumps(data, indent=2 if args.pretty else None, allow_nan=False), flush=True)
                    received += 1
            except KeyboardInterrupt:
                pass
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
