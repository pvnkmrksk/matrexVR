import os
from PIL import Image, ImageDraw, ImageFont


def create_grid_image(resolution: tuple, cell_size: int):
    # calculate the grid_length
    grid_length = min(resolution) // cell_size
    # Create a new image with the specified resolution
    img = Image.new("RGB", resolution, color="white")
    # Get a drawing context
    draw = ImageDraw.Draw(img)
    # Define the font to use for the labels
    font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 16)
    # Draw the grid lines
    for row in range(grid_length + 1):
        y = row * cell_size
        draw.line((0, y, resolution[0], y), fill="white")
    for col in range(grid_length + 1):
        x = col * cell_size
        draw.line((x, 0, x, resolution[1]), fill="white")
    # Add the labels to the center of each cell
    for row in range(grid_length):
        for col in range(grid_length):
            label = chr(ord("A") + col) + str(row + 1)
            x = (col + 0.5) * cell_size
            y = (row + 0.5) * cell_size
            draw.text((x, y), label, font=font, align="center", fill="black")
    # Save the image to a file
    img.show()
    img.save(f"grid_{grid_length}.png")


if __name__ == "__main__":
    create_grid_image((1024, 768), 128)