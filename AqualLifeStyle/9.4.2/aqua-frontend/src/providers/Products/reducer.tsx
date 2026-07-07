import type { ProductsAction } from "./actions";
import { ProductsActionTypes } from "./actions";
import type { ProductsState } from "./context";

export const productsReducer = (
  state: ProductsState,
  action: ProductsAction,
): ProductsState => {
  switch (action.type) {
    case ProductsActionTypes.getProductsPending:
      return {
        ...state,
        isPending: true,
        isSuccess: false,
        isError: false,
        errorMessage: null,
      };

    case ProductsActionTypes.getProductsSuccess:
      return {
        ...state,
        isPending: false,
        isSuccess: true,
        isError: false,
        errorMessage: null,
        products: action.payload,
      };

    case ProductsActionTypes.getProductsError:
      return {
        ...state,
        isPending: false,
        isSuccess: false,
        isError: true,
        errorMessage: action.payload,
      };

    default:
      return state;
  }
};
