
import pandas as pd
import plotly.graph_objects as go
from plotly.subplots import make_subplots
import numpy as np
import os
from typing import List

def load_csv(file_path: str) -> pd.DataFrame:
    try:
        df = pd.read_csv(file_path, parse_dates=['Current Time']).sort_values('Current Time')
        return df
    except Exception as e:
        print(f"Error loading {file_path}: {e}")
        return pd.DataFrame()

def process_dataframe(df: pd.DataFrame, trim_seconds: float = 1.0) -> pd.DataFrame:
    if df.empty:
        return df
    df['elapsed_time'] = (df['Current Time'] - df['Current Time'].min()).dt.total_seconds()
    df['CurrentTrial'] = df['CurrentTrial'].astype(int)
    df['CurrentStep'] = df['CurrentStep'].astype(int)
    df['VR'] = df['VR'].astype(str)
    df = df.sort_values(['CurrentTrial', 'CurrentStep', 'elapsed_time'])
    grouped = df.groupby(['CurrentTrial', 'CurrentStep'])
    df = grouped.apply(lambda x: x[
        (x['elapsed_time'] >= trim_seconds) &
        (x['elapsed_time'] <= x['elapsed_time'].max() - trim_seconds)
    ]).reset_index(drop=True)
    df = df[(df['GameObjectPosX'] != 0) | (df['GameObjectPosZ'] != 0)]
    return df

def add_inferred_loops(df: pd.DataFrame) -> pd.DataFrame:
    if df.empty:
        return df
    if 'loopIndex' in df.columns:
        df['plotLoop'] = df['loopIndex']
    else:
        # derive loop per VR by detecting stepIndex resets
        loops = []
        for vr, g in df.groupby('VR'):
            g = g.sort_values('Current Time')
            loop = 0
            prev_step = None
            loop_vals = []
            for s in g['stepIndex']:
                if prev_step is not None and s < prev_step:
                    loop += 1
                loop_vals.append(loop)
                prev_step = s
            g = g.copy()
            g['plotLoop'] = loop_vals
            loops.append(g)
        df = pd.concat(loops, ignore_index=True)
    df['plotKey'] = df.apply(lambda r: f"{int(r.stepIndex)}_L{int(r.plotLoop)}", axis=1)
    return df

def create_subplot_layout(n_plots: int) -> (int, int):
    n_cols = 2
    n_rows = (n_plots + n_cols - 1) // n_cols
    return n_rows, n_cols

def create_combined_figure(df: pd.DataFrame, subfolder_name: str, output_dir: str):
    step_names = df['stepName'].unique()
    n_rows, n_cols = create_subplot_layout(len(step_names))
    fig = make_subplots(
        rows=n_rows,
        cols=n_cols,
        subplot_titles=step_names,
        shared_xaxes=True,
        shared_yaxes=True,
        horizontal_spacing=0.02,
        vertical_spacing=0.03
    )
    vr_colors = {'VR1': 'blue', 'VR2': 'green', 'VR3': 'red', 'VR4': 'orange'}
    vr_legend = set()
    for i, step_name in enumerate(step_names):
        step_data = df[df['stepName'] == step_name]
        row, col = i // n_cols + 1, i % n_cols + 1
        for vr in step_data['VR'].unique():
            vr_df = step_data[step_data['VR'] == vr]
            showlegend = vr not in vr_legend
            if showlegend:
                vr_legend.add(vr)
            for key in vr_df['plotKey'].unique():
                trial_df = vr_df[vr_df['plotKey'] == key]
                fig.add_trace(go.Scatter(
                    x=trial_df['GameObjectPosX'],
                    y=trial_df['GameObjectPosZ'],
                    mode='lines',
                    name=f"{vr} - {key}" if showlegend else None,
                    line=dict(color=vr_colors.get(vr, 'black')),
                    showlegend=showlegend
                ), row=row, col=col)
                showlegend = False
    all_x, all_y = df['GameObjectPosX'], df['GameObjectPosZ']
    x_center, y_center = (all_x.min() + all_x.max()) / 2, (all_y.min() + all_y.max()) / 2
    range_max = max(all_x.max() - all_x.min(), all_y.max() - all_y.min())
    overall_min = min(x_center - range_max / 2, y_center - range_max / 2)
    overall_max = max(x_center + range_max / 2, y_center + range_max / 2)
    for i in range(len(step_names)):
        row, col = i // n_cols + 1, i % n_cols + 1
        fig.update_xaxes(title_text='X Position', range=[overall_min, overall_max], scaleanchor='y', scaleratio=1, row=row, col=col)
        fig.update_yaxes(title_text='Z Position', range=[overall_min, overall_max], scaleanchor='x', scaleratio=1, row=row, col=col)
    fig.update_layout(
        height=600 * n_rows,
        width=600 * n_cols,
        title_text=f"Ant Positions by Step - {subfolder_name}"
    )
    output_file = os.path.join(output_dir, f"{subfolder_name}_ant_positions_by_step.html")
    fig.write_html(output_file, include_plotlyjs='cdn')
    print(f"Saved combined plot at {output_file}")

def main(directory: str, trim_seconds: float = 1.0):
    subdirectories = [os.path.join(directory, d) for d in os.listdir(directory) if os.path.isdir(os.path.join(directory, d))]
    for subdir in subdirectories:
        subfolder_name = os.path.basename(subdir)
        print(f"Processing subfolder: {subfolder_name}")
        file_paths = [os.path.join(subdir, f) for f in os.listdir(subdir) if f.endswith('.csv') and not f.endswith('ColorDriftLogger_.csv')]
        if not file_paths:
            print(f"No relevant CSV files in {subdir}")
            continue
        dfs = [process_dataframe(load_csv(f), trim_seconds) for f in file_paths]
        dfs = [df for df in dfs if not df.empty]
        if not dfs:
            print(f"No valid dataframes in {subdir}")
            continue
        combined_df = add_inferred_loops(pd.concat(dfs, ignore_index=True))
        if 'stepName' not in combined_df.columns:
            print("Missing 'stepName' in data. Skipping.")
            continue
        create_combined_figure(combined_df, subfolder_name, subdir)

if __name__ == '__main__':
    import sys
    if len(sys.argv) > 1:
        main(sys.argv[1])
    else:
        print("Please provide the directory path as an argument.")
