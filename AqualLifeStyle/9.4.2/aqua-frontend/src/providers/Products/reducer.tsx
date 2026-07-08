import type { ProductsAction } from "./actions";
import { ProductsActionTypes } from "./actions";
import type { ProductsState } from "./context";

export const productsReducer = (
  state: ProductsState,
  action: ProductsAction,
): ProductsState => {
  switch (action.type) {
    case ProductsActionTypes.getEligibleProductsPending:
      return {
        ...state,
        eligibleErrorMessage: null,
        isEligibleError: false,
        isEligiblePending: true,
        isEligibleSuccess: false,
      };

    case ProductsActionTypes.getEligibleProductsSuccess:
      return {
        ...state,
        eligibleErrorMessage: null,
        eligibleProducts: action.payload,
        isEligibleError: false,
        isEligiblePending: false,
        isEligibleSuccess: true,
      };

    case ProductsActionTypes.getEligibleProductsError:
      return {
        ...state,
        eligibleErrorMessage: action.payload,
        eligibleProducts: [],
        isEligibleError: true,
        isEligiblePending: false,
        isEligibleSuccess: false,
      };

    case ProductsActionTypes.getProductPending:
      return {
        ...state,
        isSelectedPending: true,
        isSelectedSuccess: false,
        isSelectedError: false,
        selectedErrorMessage: null,
      };

    case ProductsActionTypes.getProductSuccess:
      return {
        ...state,
        isSelectedPending: false,
        isSelectedSuccess: true,
        isSelectedError: false,
        selectedErrorMessage: null,
        selectedProduct: action.payload,
      };

    case ProductsActionTypes.getProductError:
      return {
        ...state,
        isSelectedPending: false,
        isSelectedSuccess: false,
        isSelectedError: true,
        selectedErrorMessage: action.payload,
        selectedProduct: null,
      };

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
