use chess::*;

use crate::index::*;

#[derive(Debug)]
pub struct Position {
    pub features: Vec<Feature>,
    pub label: f32,
}

impl Position {
    pub fn new(board: Board, label: f32) -> Self {
        let mut features = vec![];
        Self::add_material_features(&mut features, &board);
        Self::add_psqt_features(&mut features, &board);

        Self { features, label }
    }

    fn add_material_features(features: &mut Vec<Feature>, board: &Board) {
        for piece_idx in 0..5 {
            let piece = piece_idx.into();

            let white = board.of(piece, Color::White).count() as f32;
            let black = board.of(piece, Color::Black).count() as f32;

            let coefficient = white - black;
            if coefficient != 0.0 {
                let mg_index = IDX_MATERIAL_MG + piece_idx;
                features.push(Feature::new(coefficient * board.phase, mg_index));

                let eg_index = IDX_MATERIAL_EG + piece_idx;
                features.push(Feature::new(coefficient * (1.0 - board.phase), eg_index));
            }
        }
    }

    fn add_psqt_features(features: &mut Vec<Feature>, board: &Board) {
        for square_idx in 0..64 {
            let square = square_idx.into();
            for piece_idx in 0..6 {
                let piece = piece_idx.into();

                let white = board.of(piece, Color::White).contains(square) as i8;
                let black = board.of(piece, Color::Black).contains(square ^ 56) as i8;
                let coefficient = (white - black) as f32;

                if coefficient != 0.0 {
                    let mg_index = IDX_PSQT_MG + piece_idx * 64 + square_idx;
                    features.push(Feature::new(coefficient * board.phase, mg_index));

                    let eg_index = IDX_PSQT_EG + piece_idx * 64 + square_idx;
                    features.push(Feature::new(coefficient * (1.0 - board.phase), eg_index));
                }
            }
        }
    }
}

#[derive(Debug)]
pub struct Feature {
    /// Coefficient of the feature
    pub coefficient: f32,
    /// Index of the feature in the weight vector
    pub index: usize,
}

impl Feature {
    pub fn new(coefficient: f32, index: usize) -> Self {
        Self { coefficient, index }
    }
}
