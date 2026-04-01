#!/usr/bin/env python3
"""
randomize_sequences.py

Reads an input JSON file containing a list of sequences, then generates one or more
output JSON files where the sequences are randomized and repeated.

Usage:
  python randomize_sequences.py input.json -n 4 -m 3 -o randomized

This will read `input.json`, repeat and randomize the sequences 4 times per file,
producing 3 files named `randomized_1.json`, `randomized_2.json`, and `randomized_3.json`.

Options:
  -n, --repeats        Number of times to repeat the sequence rounds (default: 1)
  -m, --files          Number of output JSON files to generate (default: 1)
  --seed               Optional integer seed for reproducibility
  -o, --output-prefix  Prefix for output files (default: "sequenceConfig")
"""
import argparse
import json
import random
import copy
import os

def parse_args():
    parser = argparse.ArgumentParser(
        description="Generate randomized repeated sequences JSON files.")
    parser.add_argument(
        "input_file", metavar="INPUT", help="Path to input JSON file.")
    parser.add_argument(
        "-r", "--repeats",
        type=int,
        default=1,
        help="Number of times to repeat the sequence rounds.")
    parser.add_argument(
        "-f", "--files",
        type=int,
        default=1,
        help="Number of output JSON files to generate.")
    parser.add_argument(
        "--seed",
        type=int,
        help="Optional random seed for reproducibility.")
    parser.add_argument(
        "-o", "--output-prefix",
        default="sequenceConfig",
        help="Prefix for output files. Files will be named PREFIX_1.json, etc.")
    return parser.parse_args()


def load_sequences(path):
    with open(path, "r") as f:
        data = json.load(f)
    sequences = data.get("sequences")
    if not isinstance(sequences, list):
        raise ValueError("Input JSON must contain a 'sequences' list.")
    return sequences


def generate_randomized(sequences, repeats):
    """
    Returns a new list of sequences where the original list is shuffled
    and appended, repeated `repeats` times.
    """
    all_seqs = []
    for _ in range(repeats):
        batch = copy.deepcopy(sequences)
        random.shuffle(batch)
        all_seqs.extend(batch)
    return all_seqs


def main():
    args = parse_args()
    base_sequences = load_sequences(args.input_file)

    for file_idx in range(1, args.files + 1):
        # Use a unique seed per file if provided
        if args.seed is not None:
            random.seed(args.seed + file_idx)
        else:
            random.seed()

        randomized = generate_randomized(base_sequences, args.repeats)
        output_data = {"sequences": randomized}

        out_name = f"{args.output_prefix}_{file_idx}.json"
        with open(out_name, "w") as out_f:
            json.dump(output_data, out_f, indent=2)
        print(f"Wrote {out_name}")

if __name__ == "__main__":
    main()
